using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

/// <summary>Talks to the MenuItems table directly through the Azure Tables SDK.</summary>
public sealed class MenuService : TableBackedService, IMenuService
{
    private readonly ILogger<MenuService> _logger;

    public MenuService(IConfiguration config, ILogger<MenuService> logger)
        : base(new TableClient(StorageSettings.Primary(config), "MenuItems"))
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<MenuItemEntity>> GetAllAsync(CancellationToken ct = default)
    {
        var table = await ReadyTableAsync(ct);
        var items = new List<MenuItemEntity>();

        await foreach (var item in table.QueryAsync<MenuItemEntity>(cancellationToken: ct))
            items.Add(item);

        return items;
    }

    public async Task<IReadOnlyList<MenuItemEntity>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        var table = await ReadyTableAsync(ct);
        var items = new List<MenuItemEntity>();

        // CreateQueryFilter escapes the value, so a category like  x' or 'a'='a  cannot alter the query.
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {category}");

        await foreach (var item in table.QueryAsync<MenuItemEntity>(filter, cancellationToken: ct))
            items.Add(item);

        return items;
    }

    public async Task<MenuItemEntity?> GetAsync(string category, string sku, CancellationToken ct = default)
    {
        var table = await ReadyTableAsync(ct);
        var result = await table.GetEntityIfExistsAsync<MenuItemEntity>(category, sku, cancellationToken: ct);
        return result.HasValue ? result.Value : null;
    }

    public async Task<MenuItemEntity> CreateAsync(MenuItemEntity item, CancellationToken ct = default)
    {
        var table = await ReadyTableAsync(ct);

        try
        {
            await table.AddEntityAsync(item, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new ConflictException($"A menu item with SKU '{item.RowKey}' already exists in '{item.PartitionKey}'.");
        }

        _logger.LogInformation("Menu item {Sku} created in {Category}.", item.RowKey, item.PartitionKey);
        return item;
    }

    public async Task<MenuItemEntity?> UpdateAsync(string category, string sku, UpdateMenuItemDto changes, CancellationToken ct = default)
    {
        var table = await ReadyTableAsync(ct);
        var existing = await table.GetEntityIfExistsAsync<MenuItemEntity>(category, sku, cancellationToken: ct);
        if (!existing.HasValue) return null;

        MenuItemEntity item = existing.Value!;

        // Only overwrite the fields the client actually sent (partial update).
        if (changes.Name is not null) item.Name = changes.Name.Trim();
        if (changes.Description is not null) item.Description = changes.Description.Trim();
        if (changes.Price is not null) item.Price = (double)changes.Price.Value;
        if (changes.IsAvailable is not null) item.IsAvailable = changes.IsAvailable.Value;

        // The ETag makes this fail (HTTP 412 → 409 via middleware) if someone else changed the row meanwhile.
        await table.UpdateEntityAsync(item, item.ETag, TableUpdateMode.Replace, ct);

        _logger.LogInformation("Menu item {Sku} in {Category} updated.", sku, category);
        return item;
    }

    public async Task<bool> DeleteAsync(string category, string sku, CancellationToken ct = default)
    {
        var table = await ReadyTableAsync(ct);

        try
        {
            Response response = await table.DeleteEntityAsync(category, sku, cancellationToken: ct);
            if (response.Status == 404) return false;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }

        _logger.LogInformation("Menu item {Sku} in {Category} deleted.", sku, category);
        return true;
    }
}
