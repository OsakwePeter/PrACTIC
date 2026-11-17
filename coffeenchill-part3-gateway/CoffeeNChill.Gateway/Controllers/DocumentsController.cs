using CoffeeNChill.Gateway.Auth;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Mapping;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Staff documents (recipes, manuals, policies). Every endpoint needs an Admin or Staff token.</summary>
[ApiController]
[Route("api/documents")]
[Authorize(Roles = AppRoles.AdminOrStaff)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public class DocumentsController : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider MimeTypes = new();

    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents) => _documents = documents;

    /// <summary>Uploads a PDF, DOC or DOCX (max 10 MB) to the staff-docs file share.</summary>
    /// <param name="file">The document, sent as multipart/form-data under the field name "file".</param>
    /// <response code="201">Uploaded; the Location header points to the download URL.</response>
    /// <response code="400">Missing, empty, oversized or wrong-type file.</response>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(StaffDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StaffDocumentDto>> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        string safeName = UploadValidator.Validate(file.FileName, file.ContentType, file.Length);

        await using Stream content = file.OpenReadStream();
        var stored = await _documents.UploadAsync(safeName, file.ContentType, content, file.Length, ct);

        return CreatedAtAction(nameof(Download), new { fileName = stored.FileName }, stored.ToDto());
    }

    /// <summary>Lists all documents with name, size and last-modified date.</summary>
    /// <response code="200">The documents (empty list if none).</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StaffDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<StaffDocumentDto>>> List(CancellationToken ct)
    {
        var docs = await _documents.ListAsync(ct);
        return Ok(docs.Select(d => d.ToDto()));
    }

    /// <summary>Streams a document back to the client.</summary>
    /// <param name="fileName">Exact file name, e.g. "espresso-recipe.pdf".</param>
    /// <response code="200">The file content.</response>
    /// <response code="404">No such document.</response>
    [HttpGet("download/{fileName}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string fileName, CancellationToken ct)
    {
        string safeName = Path.GetFileName(fileName);

        Stream stream = await _documents.DownloadAsync(safeName, ct)
                        ?? throw new NotFoundException($"Document '{safeName}' was not found.");

        if (!MimeTypes.TryGetContentType(safeName, out string? contentType))
            contentType = "application/octet-stream";

        return File(stream, contentType, safeName);
    }

    /// <summary>Deletes a document.</summary>
    /// <param name="fileName">Exact file name.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No such document.</response>
    [HttpDelete("{fileName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string fileName, CancellationToken ct)
    {
        string safeName = Path.GetFileName(fileName);

        if (!await _documents.DeleteAsync(safeName, ct))
            throw new NotFoundException($"Document '{safeName}' was not found.");

        return NoContent();
    }
}
