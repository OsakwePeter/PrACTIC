using System.Text.Json;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Mapping;

/// <summary>
/// Hand-written DTO ⇄ model mapping. Controllers only ever see DTOs on the wire; storage
/// entities never leak out of the gateway. (No AutoMapper: the mapping is small and explicit.)
/// </summary>
public static class MappingExtensions
{
    // ---- Menu ----
    public static MenuItemEntity ToEntity(this CreateMenuItemDto dto) => new()
    {
        PartitionKey = dto.Category.Trim(),
        RowKey = dto.Sku.Trim(),
        Name = dto.Name.Trim(),
        Description = dto.Description.Trim(),
        Price = (double)dto.Price,
        IsAvailable = dto.IsAvailable
    };

    public static MenuItemDto ToDto(this MenuItemEntity entity) => new()
    {
        Category = entity.PartitionKey,
        Sku = entity.RowKey,
        Name = entity.Name,
        Description = entity.Description,
        Price = Convert.ToDecimal(entity.Price),
        IsAvailable = entity.IsAvailable
    };

    // ---- Orders ----
    /// <summary>Builds the queue message. Generates OrderId / timestamp when the client left them out.</summary>
    public static QueuedOrder ToQueuedOrder(this CreateOrderDto dto, DateTimeOffset now) => new()
    {
        OrderId = string.IsNullOrWhiteSpace(dto.OrderId)
            ? $"ORD-{now.Year}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
            : dto.OrderId.Trim(),
        CustomerName = dto.CustomerName.Trim(),
        SelectedItemSKUs = dto.SelectedItemSKUs.Select(s => s.Trim()).ToList(),
        TotalPrice = dto.TotalPrice,
        OrderTimestamp = dto.OrderTimestamp?.ToUniversalTime() ?? now.ToUniversalTime()
    };

    public static QueuedOrderResponseDto ToResponse(this QueuedOrder order) => new()
    {
        OrderId = order.OrderId,
        OrderTimestamp = order.OrderTimestamp
    };

    public static OrderStatusDto ToDto(this OrderEntity entity) => new()
    {
        OrderId = entity.OrderId,
        OrderDate = entity.PartitionKey,
        CustomerName = entity.CustomerName,
        SelectedItemSKUs = ParseSkus(entity.SelectedItemSKUs),
        TotalPrice = Convert.ToDecimal(entity.TotalPrice),
        Status = entity.Status,
        LastUpdated = entity.LastUpdated
    };

    // ---- Documents ----
    public static StaffDocumentDto ToDto(this StaffDocument doc) => new()
    {
        FileName = doc.FileName,
        ContentType = doc.ContentType,
        SizeBytes = doc.SizeBytes,
        LastModified = doc.LastModified
    };

    /// <summary>The Orders table stores SKUs as a JSON array string; a bad value yields an empty list.</summary>
    private static List<string> ParseSkus(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
