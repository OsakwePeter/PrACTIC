using System.Text.Json;
using Azure;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CoffeeNChill.Gateway.Tests;

public class ExceptionMiddlewareTests
{
    /// <summary>Runs the middleware around a delegate that throws, and returns the HTTP context.</summary>
    private static async Task<DefaultHttpContext> RunAsync(Exception toThrow, string environment = "Production")
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw toThrow, NullLogger<ExceptionHandlingMiddleware>.Instance, env);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);
        return context;
    }

    private static JsonElement ReadBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var doc = JsonDocument.Parse(context.Response.Body);
        return doc.RootElement.Clone();
    }

    [Theory]
    [InlineData(typeof(NotFoundException), 404)]
    [InlineData(typeof(ConflictException), 409)]
    [InlineData(typeof(ApiValidationException), 400)]
    public async Task ApiExceptions_AreMappedToTheirStatusCodes(Type exceptionType, int expected)
    {
        var ex = (Exception)Activator.CreateInstance(exceptionType, "something went wrong")!;

        var context = await RunAsync(ex);

        Assert.Equal(expected, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("something went wrong", ReadBody(context).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ServiceUnavailable_Returns503()
    {
        var context = await RunAsync(new ServiceUnavailableException("queue down"));
        Assert.Equal(503, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(404, 404)]
    [InlineData(409, 409)]
    [InlineData(412, 409)]
    [InlineData(0, 503)]
    [InlineData(500, 502)]
    public async Task StorageErrors_AreTranslatedToGatewayStatusCodes(int storageStatus, int expected)
    {
        var context = await RunAsync(new RequestFailedException(storageStatus, "storage said no"));
        Assert.Equal(expected, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedException_Returns500_AndDoesNotLeakInternals_InProduction()
    {
        var context = await RunAsync(new InvalidOperationException("secret connection string AccountKey=abc"));

        Assert.Equal(500, context.Response.StatusCode);

        string body = ReadBody(context).ToString();
        Assert.DoesNotContain("AccountKey", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task ProblemResponse_ContainsTraceId()
    {
        var context = await RunAsync(new NotFoundException("nope"));
        Assert.True(ReadBody(context).TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task RequestThatSucceeds_IsLeftAlone()
    {
        var env = Substitute.For<IHostEnvironment>();
        var middleware = new ExceptionHandlingMiddleware(
            ctx => { ctx.Response.StatusCode = 204; return Task.CompletedTask; },
            NullLogger<ExceptionHandlingMiddleware>.Instance, env);

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.Equal(204, context.Response.StatusCode);
    }
}
