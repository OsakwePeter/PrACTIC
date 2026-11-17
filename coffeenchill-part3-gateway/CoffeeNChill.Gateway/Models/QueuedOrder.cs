namespace CoffeeNChill.Gateway.Models;

/// <summary>
/// The JSON message placed on order-processing-queue. Property names (PascalCase) must match
/// the Order model that ProcessOrderQueue deserialises in the Functions project.
/// </summary>
public class QueuedOrder
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSKUs { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public DateTimeOffset OrderTimestamp { get; set; }
}
