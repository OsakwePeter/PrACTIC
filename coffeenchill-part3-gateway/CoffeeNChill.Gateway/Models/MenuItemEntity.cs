using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Gateway.Models;

/// <summary>
/// Row in the MenuItems Azure Table. Same schema the Part 1 Functions use:
/// PartitionKey = Category, RowKey = SKU.
/// </summary>
public class MenuItemEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;   // Category
    public string RowKey { get; set; } = string.Empty;         // SKU
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Price { get; set; }
    public bool IsAvailable { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
