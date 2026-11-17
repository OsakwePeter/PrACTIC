using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

/// <summary>Stores staff documents in the "staff-docs" Azure File Share.</summary>
public sealed class DocumentService : IDocumentService
{
    private const string ShareName = "staff-docs";

    // Azure Files accepts at most 4 MiB per range upload, so larger files are sent in chunks.
    private const int ChunkSize = 4 * 1024 * 1024;

    private readonly ShareClient _share;
    private readonly ILogger<DocumentService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _ready;

    public DocumentService(IConfiguration config, ILogger<DocumentService> logger)
    {
        _logger = logger;
        _share = new ShareClient(StorageSettings.Files(config), ShareName);
    }

    private async Task<ShareDirectoryClient> RootAsync(CancellationToken ct)
    {
        if (!_ready)
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (!_ready)
                {
                    await _share.CreateIfNotExistsAsync(cancellationToken: ct);
                    _ready = true;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        return _share.GetRootDirectoryClient();
    }

    public async Task<StaffDocument> UploadAsync(
        string fileName, string contentType, Stream content, long length, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        ShareFileClient file = root.GetFileClient(fileName);

        // Creating the file reserves its full size and sets the MIME type; uploading an existing name replaces it.
        await file.CreateAsync(length, new ShareFileCreateOptions
        {
            HttpHeaders = new ShareFileHttpHeaders { ContentType = contentType }
        }, cancellationToken: ct);

        // Stream the upload straight to the share in 4 MiB chunks (never buffers the whole file).
        var buffer = new byte[ChunkSize];
        long offset = 0;
        int read;
        while ((read = await content.ReadAsync(buffer.AsMemory(0, ChunkSize), ct)) > 0)
        {
            using var chunk = new MemoryStream(buffer, 0, read, writable: false);
            await file.UploadRangeAsync(new HttpRange(offset, read), chunk, cancellationToken: ct);
            offset += read;
        }

        _logger.LogInformation("Staff document {FileName} uploaded ({Bytes} bytes).", fileName, length);

        return new StaffDocument
        {
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = length,
            LastModified = DateTimeOffset.UtcNow
        };
    }

    public async Task<IReadOnlyList<StaffDocument>> ListAsync(CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        var documents = new List<StaffDocument>();

        await foreach (ShareFileItem item in root.GetFilesAndDirectoriesAsync(cancellationToken: ct))
        {
            if (item.IsDirectory) continue;

            ShareFileProperties props = (await root.GetFileClient(item.Name).GetPropertiesAsync(ct)).Value;
            documents.Add(new StaffDocument
            {
                FileName = item.Name,
                ContentType = props.ContentType ?? string.Empty,
                SizeBytes = props.ContentLength,
                LastModified = props.LastModified
            });
        }

        return documents;
    }

    public async Task<Stream?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        ShareFileClient file = root.GetFileClient(fileName);

        if (!await file.ExistsAsync(ct)) return null;

        // OpenRead streams the content as the caller reads it, so large files are not held in memory.
        return await file.OpenReadAsync(cancellationToken: ct);
    }

    public async Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var root = await RootAsync(ct);
        Response<bool> result = await root.GetFileClient(fileName).DeleteIfExistsAsync(cancellationToken: ct);

        if (result.Value) _logger.LogInformation("Staff document {FileName} deleted.", fileName);
        return result.Value;
    }
}
