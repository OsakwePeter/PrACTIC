using System.ComponentModel.DataAnnotations;

namespace CoffeeNChill.Gateway.DTOs;

/// <summary>Request body for creating a menu item.</summary>
public class CreateMenuItemDto
{
    private const string KeyPattern = @"^[^/\\#?\r\n\t]+$";
    private const string KeyMessage = "{0} cannot contain / \\ # ? or control characters (Azure Table key rules).";

    /// <summary>Menu category, e.g. "Hot Drinks". Becomes the table PartitionKey.</summary>
    /// <example>Hot Drinks</example>
    [Required, StringLength(50, MinimumLength = 1)]
    [RegularExpression(KeyPattern, ErrorMessage = KeyMessage)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Unique item SKU, e.g. "COF-001". Becomes the table RowKey.</summary>
    /// <example>COF-001</example>
    [Required, StringLength(30, MinimumLength = 1)]
    [RegularExpression(KeyPattern, ErrorMessage = KeyMessage)]
    public string Sku { get; set; } = string.Empty;

    /// <example>Espresso</example>
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <example>Double shot of house blend</example>
    [Required, StringLength(300, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Price in rand. Must be greater than zero.</summary>
    /// <example>32.50</example>
    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    /// <summary>Whether the item can currently be ordered.</summary>
    public bool IsAvailable { get; set; } = true;
}

/// <summary>
/// Request body for updating a menu item. Send only the fields that change;
/// omitted fields keep their current value. At least one field is required.
/// </summary>
public class UpdateMenuItemDto : IValidatableObject
{
    /// <example>Espresso (double)</example>
    [StringLength(100)]
    public string? Name { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    /// <example>35.00</example>
    [Range(0.01, 100000)]
    public decimal? Price { get; set; }

    public bool? IsAvailable { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name is null && Description is null && Price is null && IsAvailable is null)
            yield return new ValidationResult("Provide at least one of Name, Description, Price or IsAvailable.");

        if (Name is not null && string.IsNullOrWhiteSpace(Name))
            yield return new ValidationResult("Name cannot be blank.", new[] { nameof(Name) });

        if (Description is not null && string.IsNullOrWhiteSpace(Description))
            yield return new ValidationResult("Description cannot be blank.", new[] { nameof(Description) });
    }
}

/// <summary>A menu item as returned to clients.</summary>
public class MenuItemDto
{
    public string Category { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}
