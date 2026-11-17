namespace CoffeeNChill.Gateway.Models;

/// <summary>A file stored in the staff-docs Azure File Share.</summary>
public class StaffDocument
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset LastModified { get; set; }
}
