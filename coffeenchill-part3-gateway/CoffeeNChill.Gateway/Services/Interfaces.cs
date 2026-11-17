using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

/// <summary>Menu storage (Azure Table "MenuItems").</summary>
public interface IMenuService
{
    Task<IReadOnlyList<MenuItemEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MenuItemEntity>> GetByCategoryAsync(string category, CancellationToken ct = default);
    Task<MenuItemEntity?> GetAsync(string category, string sku, CancellationToken ct = default);

    /// <exception cref="Exceptions.ConflictException">An item with this category + SKU already exists.</exception>
    Task<MenuItemEntity> CreateAsync(MenuItemEntity item, CancellationToken ct = default);

    /// <returns>The updated item, or null if it does not exist.</returns>
    Task<MenuItemEntity?> UpdateAsync(string category, string sku, UpdateMenuItemDto changes, CancellationToken ct = default);

    /// <returns>True if deleted, false if it did not exist.</returns>
    Task<bool> DeleteAsync(string category, string sku, CancellationToken ct = default);
}

/// <summary>Order queue producer (Azure Queue) and order tracking (Azure Table "Orders").</summary>
public interface IOrderService
{
    /// <exception cref="Exceptions.ServiceUnavailableException">The queue cannot be reached.</exception>
    Task EnqueueAsync(QueuedOrder order, CancellationToken ct = default);

    Task<OrderEntity?> GetAsync(string orderId, CancellationToken ct = default);

    /// <returns>The updated order, or null if it does not exist.</returns>
    /// <exception cref="Exceptions.ApiValidationException">Unknown status name.</exception>
    /// <exception cref="Exceptions.ConflictException">Status would not move forward.</exception>
    Task<OrderEntity?> UpdateStatusAsync(string orderId, string status, CancellationToken ct = default);
}

/// <summary>Staff documents (Azure File Share "staff-docs").</summary>
public interface IDocumentService
{
    Task<StaffDocument> UploadAsync(string fileName, string contentType, Stream content, long length, CancellationToken ct = default);
    Task<IReadOnlyList<StaffDocument>> ListAsync(CancellationToken ct = default);

    /// <returns>A readable stream, or null if the file does not exist.</returns>
    Task<Stream?> DownloadAsync(string fileName, CancellationToken ct = default);

    /// <returns>True if deleted, false if it did not exist.</returns>
    Task<bool> DeleteAsync(string fileName, CancellationToken ct = default);
}
