namespace CoffeeNChill.Gateway.DTOs;

/// <summary>Metadata for a file in the staff-docs share.</summary>
public class StaffDocumentDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long SizeBytes { get; set; }

    public DateTimeOffset LastModified { get; set; }
}
