using CoffeeNChill.Gateway.Exceptions;

namespace CoffeeNChill.Gateway.Services;

/// <summary>Rules for staff document uploads (same limits as the Part 1 Function).</summary>
public static class UploadValidator
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    /// <summary>
    /// Checks size, extension and MIME type, and returns a safe file name (path segments stripped,
    /// so "../../x.pdf" becomes "x.pdf").
    /// </summary>
    /// <exception cref="ApiValidationException">The upload breaks a rule.</exception>
    public static string Validate(string? fileName, string? contentType, long length)
    {
        string safeName = Path.GetFileName(fileName ?? string.Empty).Trim();

        if (safeName.Length == 0)
            throw new ApiValidationException("The uploaded file must have a valid file name.");

        if (length <= 0)
            throw new ApiValidationException("The uploaded file is empty.");

        if (length > MaxFileSizeBytes)
            throw new ApiValidationException("The uploaded file exceeds the maximum size of 10 MB.");

        string extension = Path.GetExtension(safeName);
        if (!AllowedTypes.TryGetValue(extension, out string? expectedType))
            throw new ApiValidationException("Invalid file type. Only PDF, DOC and DOCX files are allowed.");

        if (!string.Equals(contentType, expectedType, StringComparison.OrdinalIgnoreCase))
            throw new ApiValidationException($"Content type must be '{expectedType}' for a {extension} file.");

        return safeName;
    }
}
