using System.ComponentModel.DataAnnotations;
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.DTOs;

/// <summary>Request body for placing an order on the queue.</summary>
public class CreateOrderDto : IValidatableObject
{
    /// <summary>Optional. If omitted the gateway generates one (e.g. ORD-2026-9F3A1C2B).</summary>
    /// <example>ORD-2026-8801</example>
    [StringLength(40)]
    public string? OrderId { get; set; }

    /// <example>Jane Smith</example>
    [Required, StringLength(100, MinimumLength = 1)]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>SKUs of the ordered menu items (at least one).</summary>
    /// <example>["COF-001", "PAS-104"]</example>
    [Required, MinLength(1, ErrorMessage = "At least one SKU is required.")]
    public List<string> SelectedItemSKUs { get; set; } = new();

    /// <summary>Order total in rand. Must be greater than zero.</summary>
    /// <example>65.00</example>
    [Range(0.01, 100000)]
    public decimal TotalPrice { get; set; }

    /// <summary>Optional. Generated in UTC by the gateway if omitted.</summary>
    public DateTimeOffset? OrderTimestamp { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SelectedItemSKUs is null) yield break;

        if (SelectedItemSKUs.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 30))
            yield return new ValidationResult(
                "Every SKU must be non-empty and at most 30 characters.", new[] { nameof(SelectedItemSKUs) });
    }
}

/// <summary>Returned (HTTP 202) once an order has been accepted onto the queue.</summary>
public class QueuedOrderResponseDto
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public DateTimeOffset OrderTimestamp { get; set; }
    public string Message { get; set; } = "Order queued successfully.";
}

/// <summary>Current state of an order, read from the Orders table.</summary>
public class OrderStatusDto
{
    public string OrderId { get; set; } = string.Empty;

    /// <summary>Order date (the Orders table PartitionKey, yyyy-MM-dd).</summary>
    public string OrderDate { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSKUs { get; set; } = new();
    public decimal TotalPrice { get; set; }

    /// <summary>Received, Preparing, Ready or Collected.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>Request body for a staff member advancing an order's status.</summary>
public class UpdateOrderStatusDto : IValidatableObject
{
    /// <example>ORD-2026-8801</example>
    [Required]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>Preparing, Ready or Collected (statuses only move forward).</summary>
    /// <example>Ready</example>
    [Required]
    public string Status { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Status) && !OrderStatuses.TryNormalise(Status, out _))
            yield return new ValidationResult(
                $"Status must be one of: {string.Join(", ", OrderStatuses.All)}.", new[] { nameof(Status) });
    }
}
