namespace CoffeeNChill.Gateway.Exceptions;

/// <summary>
/// Base class for errors the gateway raises on purpose. The global exception middleware
/// turns each one into an RFC 7807 problem response with the matching HTTP status code,
/// so controllers and services never build error responses by hand.
/// </summary>
public abstract class ApiException : Exception
{
    protected ApiException(int statusCode, string title, string message, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public int StatusCode { get; }
    public string Title { get; }
}

/// <summary>The requested menu item, order or document does not exist (HTTP 404).</summary>
public sealed class NotFoundException : ApiException
{
    public NotFoundException(string message) : base(StatusCodes.Status404NotFound, "Resource not found", message) { }
}

/// <summary>The request clashes with current state, e.g. duplicate SKU or backwards status change (HTTP 409).</summary>
public sealed class ConflictException : ApiException
{
    public ConflictException(string message) : base(StatusCodes.Status409Conflict, "Conflict", message) { }
}

/// <summary>Business-rule validation failed (HTTP 400).</summary>
public sealed class ApiValidationException : ApiException
{
    public ApiValidationException(string message) : base(StatusCodes.Status400BadRequest, "Validation failed", message) { }
}

/// <summary>A backing service (queue, table, file share) could not be reached (HTTP 503).</summary>
public sealed class ServiceUnavailableException : ApiException
{
    public ServiceUnavailableException(string message, Exception? inner = null)
        : base(StatusCodes.Status503ServiceUnavailable, "Service unavailable", message, inner) { }
}
