using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using CoffeeNChill.Gateway.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Middleware;

/// <summary>
/// Global exception handler: the single place where exceptions become HTTP responses.
/// Every failure is returned as an RFC 7807 "problem details" JSON body with a traceId, and
/// internal details (stack traces, storage messages) are never leaked outside Development.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; nobody is listening for a response.
            _logger.LogInformation("Request {Path} cancelled by the client.", context.Request.Path);
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Exception after the response started; cannot write an error body.");
                throw;
            }

            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        (int status, string title, string detail) = Map(ex);

        if (status >= 500)
            _logger.LogError(ex, "Unhandled {Status} on {Method} {Path}", status, context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("Request failed with {Status} on {Method} {Path}: {Message}",
                status, context.Request.Method, context.Request.Path, ex.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = _env.IsDevelopment() && status >= 500 ? ex.ToString() : detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    /// <summary>Decides the status code and the (client-safe) message for an exception.</summary>
    private static (int Status, string Title, string Detail) Map(Exception ex) => ex switch
    {
        ApiException api => (api.StatusCode, api.Title, api.Message),

        RequestFailedException { Status: 400 } =>
            (400, "Invalid request", "The request contains a value that storage rejected."),
        RequestFailedException { Status: 404 } =>
            (404, "Resource not found", "The requested resource was not found."),
        RequestFailedException { Status: 409 or 412 } =>
            (409, "Conflict", "The resource already exists or was changed by another request. Reload and try again."),
        RequestFailedException { Status: 0 } =>
            (503, "Service unavailable", "A backing storage service is currently unreachable."),
        RequestFailedException =>
            (502, "Storage error", "The storage service returned an unexpected error."),

        HttpRequestException or TimeoutException =>
            (503, "Service unavailable", "A backing storage service is currently unreachable."),
        AggregateException agg when agg.InnerExceptions.Any(e => e is HttpRequestException or TimeoutException or RequestFailedException) =>
            (503, "Service unavailable", "A backing storage service is currently unreachable."),

        _ => (500, "Unexpected error", "An unexpected error occurred. Quote the traceId when reporting it.")
    };
}
