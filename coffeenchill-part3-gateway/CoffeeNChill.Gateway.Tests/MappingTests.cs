using System.Text.Json;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Mapping;
using CoffeeNChill.Gateway.Models;
using Xunit;

namespace CoffeeNChill.Gateway.Tests;

public class MappingTests
{
    [Fact]
    public void CreateMenuItemDto_ToEntity_UsesCategoryAsPartitionKeyAndSkuAsRowKey()
    {
        var dto = new CreateMenuItemDto
        {
            Category = " Hot Drinks ", Sku = "COF-001", Name = "Espresso",
            Description = "Double shot", Price = 32.5m, IsAvailable = true
        };

        MenuItemEntity entity = dto.ToEntity();

        Assert.Equal("Hot Drinks", entity.PartitionKey);   // trimmed
        Assert.Equal("COF-001", entity.RowKey);
        Assert.Equal(32.5, entity.Price);
    }

    [Fact]
    public void MenuItemEntity_ToDto_MapsKeysBackToCategoryAndSku()
    {
        var entity = new MenuItemEntity
        {
            PartitionKey = "Pastries", RowKey = "PAS-104", Name = "Croissant",
            Description = "Ham & cheese", Price = 28.0, IsAvailable = false
        };

        MenuItemDto dto = entity.ToDto();

        Assert.Equal("Pastries", dto.Category);
        Assert.Equal("PAS-104", dto.Sku);
        Assert.Equal(28.0m, dto.Price);
        Assert.False(dto.IsAvailable);
    }

    [Fact]
    public void CreateOrderDto_ToQueuedOrder_GeneratesIdAndUtcTimestampWhenMissing()
    {
        var now = new DateTimeOffset(2026, 10, 12, 10, 15, 0, TimeSpan.FromHours(2));
        var dto = new CreateOrderDto
        {
            CustomerName = " Jane Smith ", SelectedItemSKUs = new() { "COF-001", " PAS-104 " }, TotalPrice = 65m
        };

        QueuedOrder order = dto.ToQueuedOrder(now);

        Assert.StartsWith("ORD-2026-", order.OrderId);
        Assert.Equal("Jane Smith", order.CustomerName);
        Assert.Equal(new[] { "COF-001", "PAS-104" }, order.SelectedItemSKUs);
        Assert.Equal(TimeSpan.Zero, order.OrderTimestamp.Offset);   // converted to UTC
    }

    [Fact]
    public void CreateOrderDto_ToQueuedOrder_KeepsClientSuppliedOrderId()
    {
        var dto = new CreateOrderDto
        {
            OrderId = "ORD-2026-8801", CustomerName = "Jane", SelectedItemSKUs = new() { "COF-001" }, TotalPrice = 10m
        };

        Assert.Equal("ORD-2026-8801", dto.ToQueuedOrder(DateTimeOffset.UtcNow).OrderId);
    }

    [Fact]
    public void QueuedOrder_SerialisesWithTheFieldNamesTheQueueFunctionExpects()
    {
        var order = new QueuedOrder
        {
            OrderId = "ORD-1", CustomerName = "Jane", SelectedItemSKUs = new() { "COF-001" },
            TotalPrice = 65m, OrderTimestamp = DateTimeOffset.UtcNow
        };

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(order));

        foreach (string field in new[] { "OrderId", "CustomerName", "SelectedItemSKUs", "TotalPrice", "OrderTimestamp" })
            Assert.True(json.RootElement.TryGetProperty(field, out _), $"Missing {field}");
    }

    [Fact]
    public void OrderEntity_ToDto_ParsesSkuJsonAndSurvivesCorruptJson()
    {
        var good = new OrderEntity { OrderId = "A", PartitionKey = "2026-10-12", SelectedItemSKUs = "[\"COF-001\",\"PAS-104\"]", TotalPrice = 65 };
        var bad = new OrderEntity { OrderId = "B", SelectedItemSKUs = "not json" };

        Assert.Equal(2, good.ToDto().SelectedItemSKUs.Count);
        Assert.Equal("2026-10-12", good.ToDto().OrderDate);
        Assert.Empty(bad.ToDto().SelectedItemSKUs);
    }
}
