using Azure;
using Azure.Data.Tables;

namespace CoffeeNChill.Gateway.Models;

/// <summary>
/// Row in the Orders Azure Table, written by the ProcessOrderQueue Function (Part 2).
/// PartitionKey = order date (yyyy-MM-dd), RowKey = OrderId.
/// </summary>
public class OrderEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>SKU list stored as a JSON array string (matches the Functions project).</summary>
    public string SelectedItemSKUs { get; set; } = "[]";

    public double TotalPrice { get; set; }
    public DateTimeOffset OrderTimestamp { get; set; }
    public string Status { get; set; } = OrderStatuses.Received;
    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>The barista lifecycle: Received → Preparing → Ready → Collected.</summary>
public static class OrderStatuses
{
    public const string Received = "Received";
    public const string Preparing = "Preparing";
    public const string Ready = "Ready";
    public const string Collected = "Collected";

    /// <summary>All statuses in lifecycle order (index = rank).</summary>
    public static readonly IReadOnlyList<string> All = new[] { Received, Preparing, Ready, Collected };

    /// <summary>Case-insensitive match; returns the canonical spelling.</summary>
    public static bool TryNormalise(string? value, out string normalised)
    {
        normalised = All.FirstOrDefault(s => string.Equals(s, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return normalised.Length > 0;
    }

    /// <summary>Position in the lifecycle (Received = 0 … Collected = 3), or -1 if unknown.</summary>
    public static int Rank(string status) => All.ToList().IndexOf(status);
}
