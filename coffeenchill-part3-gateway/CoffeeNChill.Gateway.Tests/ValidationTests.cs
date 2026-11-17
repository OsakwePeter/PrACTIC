using System.ComponentModel.DataAnnotations;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Models;
using CoffeeNChill.Gateway.Services;
using Xunit;

namespace CoffeeNChill.Gateway.Tests;

public class ValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    // ---- DTO validation ----
    [Fact]
    public void CreateMenuItemDto_Valid_PassesValidation()
    {
        var dto = new CreateMenuItemDto { Category = "Hot Drinks", Sku = "COF-001", Name = "Espresso", Description = "Strong", Price = 30m };
        Assert.Empty(Validate(dto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CreateMenuItemDto_NonPositivePrice_Fails(double price)
    {
        var dto = new CreateMenuItemDto { Category = "Hot Drinks", Sku = "COF-001", Name = "Espresso", Description = "Strong", Price = (decimal)price };
        Assert.NotEmpty(Validate(dto));
    }

    [Theory]
    [InlineData("COF/001")]
    [InlineData("COF#001")]
    [InlineData("COF?001")]
    public void CreateMenuItemDto_SkuWithIllegalTableKeyCharacters_Fails(string sku)
    {
        var dto = new CreateMenuItemDto { Category = "Hot Drinks", Sku = sku, Name = "Espresso", Description = "Strong", Price = 30m };
        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void UpdateMenuItemDto_WithNoFields_Fails()
        => Assert.NotEmpty(Validate(new UpdateMenuItemDto()));

    [Fact]
    public void UpdateMenuItemDto_PriceOnly_Passes()
        => Assert.Empty(Validate(new UpdateMenuItemDto { Price = 35m }));

    [Fact]
    public void CreateOrderDto_NoSkus_Fails()
    {
        var dto = new CreateOrderDto { CustomerName = "Jane", SelectedItemSKUs = new(), TotalPrice = 10m };
        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void CreateOrderDto_BlankSku_Fails()
    {
        var dto = new CreateOrderDto { CustomerName = "Jane", SelectedItemSKUs = new() { "COF-001", " " }, TotalPrice = 10m };
        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void UpdateOrderStatusDto_UnknownStatus_Fails()
        => Assert.NotEmpty(Validate(new UpdateOrderStatusDto { OrderId = "ORD-1", Status = "Teleported" }));

    [Fact]
    public void UpdateOrderStatusDto_StatusIsCaseInsensitive()
        => Assert.Empty(Validate(new UpdateOrderStatusDto { OrderId = "ORD-1", Status = "ready" }));

    // ---- Order status lifecycle ----
    [Fact]
    public void OrderStatuses_RankFollowsTheBaristaLifecycle()
    {
        Assert.True(OrderStatuses.Rank("Received") < OrderStatuses.Rank("Preparing"));
        Assert.True(OrderStatuses.Rank("Preparing") < OrderStatuses.Rank("Ready"));
        Assert.True(OrderStatuses.Rank("Ready") < OrderStatuses.Rank("Collected"));
    }

    [Fact]
    public void OrderStatuses_TryNormalise_ReturnsCanonicalSpelling()
    {
        Assert.True(OrderStatuses.TryNormalise("  pREPARING ", out string result));
        Assert.Equal("Preparing", result);
        Assert.False(OrderStatuses.TryNormalise("nope", out _));
    }

    // ---- Upload validation ----
    [Fact]
    public void UploadValidator_AcceptsPdf()
        => Assert.Equal("recipe.pdf", UploadValidator.Validate("recipe.pdf", "application/pdf", 1024));

    [Fact]
    public void UploadValidator_StripsPathSegmentsFromFileName()
        => Assert.Equal("evil.pdf", UploadValidator.Validate("../../evil.pdf", "application/pdf", 1024));

    [Fact]
    public void UploadValidator_RejectsDisallowedExtension()
        => Assert.Throws<ApiValidationException>(() => UploadValidator.Validate("virus.exe", "application/pdf", 1024));

    [Fact]
    public void UploadValidator_RejectsMismatchedContentType()
        => Assert.Throws<ApiValidationException>(() => UploadValidator.Validate("recipe.pdf", "text/plain", 1024));

    [Fact]
    public void UploadValidator_RejectsEmptyAndOversizedFiles()
    {
        Assert.Throws<ApiValidationException>(() => UploadValidator.Validate("a.pdf", "application/pdf", 0));
        Assert.Throws<ApiValidationException>(() =>
            UploadValidator.Validate("a.pdf", "application/pdf", UploadValidator.MaxFileSizeBytes + 1));
    }
}
