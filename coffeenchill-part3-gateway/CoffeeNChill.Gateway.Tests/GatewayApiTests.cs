using System.Net;
using System.Net.Http.Json;
using CoffeeNChill.Gateway.Models;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Xunit;

namespace CoffeeNChill.Gateway.Tests;

/// <summary>Boots the real gateway in memory with the storage services replaced by fakes (no Azurite needed).</summary>
public class GatewayFactory : WebApplicationFactory<Program>
{
    public IMenuService Menu { get; } = Substitute.For<IMenuService>();
    public IOrderService Orders { get; } = Substitute.For<IOrderService>();
    public IDocumentService Documents { get; } = Substitute.For<IDocumentService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production so error responses hide exception details, like they will on the VPS.
        builder.UseEnvironment("Production");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMenuService>();
            services.RemoveAll<IOrderService>();
            services.RemoveAll<IDocumentService>();
            services.AddSingleton(Menu);
            services.AddSingleton(Orders);
            services.AddSingleton(Documents);
        });
    }
}

public class GatewayApiTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;
    private readonly HttpClient _client;

    public GatewayApiTests(GatewayFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMenu_WithoutToken_Returns200WithItems()
    {
        _factory.Menu.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<MenuItemEntity>
        {
            new() { PartitionKey = "Hot Drinks", RowKey = "COF-001", Name = "Espresso", Description = "Strong", Price = 30, IsAvailable = true }
        });

        var response = await _client.GetAsync("/api/menu");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("COF-001", body);
        Assert.Contains("Hot Drinks", body);
    }

    [Fact]
    public async Task GetMenuItem_Missing_Returns404ProblemJson()
    {
        _factory.Menu.GetAsync("Hot Drinks", "NOPE", Arg.Any<CancellationToken>()).Returns((MenuItemEntity?)null);

        var response = await _client.GetAsync("/api/menu/Hot%20Drinks/NOPE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostMenu_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/menu", new
        {
            category = "Hot Drinks", sku = "COF-009", name = "Mocha", description = "Choc", price = 40, isAvailable = true
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        _ = _factory.Menu.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task DeleteMenu_WithoutToken_Returns401()
    {
        var response = await _client.DeleteAsync("/api/menu/Hot%20Drinks/COF-001");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListDocuments_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithoutToken_Returns401()
    {
        var response = await _client.PutAsJsonAsync("/api/orders/status", new { orderId = "ORD-1", status = "Ready" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QueueOrder_WithoutToken_ValidOrder_Returns202AndQueuesIt()
    {
        var response = await _client.PostAsJsonAsync("/api/orders/queue", new
        {
            orderId = "ORD-2026-8801", customerName = "Jane Smith",
            selectedItemSKUs = new[] { "COF-001", "PAS-104" }, totalPrice = 65.00
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        _ = _factory.Orders.Received().EnqueueAsync(
            Arg.Is<QueuedOrder>(o => o.OrderId == "ORD-2026-8801" && o.SelectedItemSKUs.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueueOrder_InvalidOrder_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders/queue", new
        {
            customerName = "", selectedItemSKUs = Array.Empty<string>(), totalPrice = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_Unknown_Returns404()
    {
        _factory.Orders.GetAsync("ORD-GHOST", Arg.Any<CancellationToken>()).Returns((OrderEntity?)null);

        var response = await _client.GetAsync("/api/orders/ORD-GHOST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedFailure_Returns500ProblemJson_WithoutLeakingTheMessage()
    {
        _factory.Menu.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<MenuItemEntity>>(_ => throw new InvalidOperationException("AccountKey=super-secret"));

        var response = await _client.GetAsync("/api/menu");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("super-secret", body);
    }

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerJson_IsServed_AndListsTheGatewayRoutes()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        string json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/menu", json);
        Assert.Contains("/api/orders/queue", json);
        Assert.Contains("/api/documents/upload", json);
    }
}
