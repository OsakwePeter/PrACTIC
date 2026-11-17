using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Models;

namespace CoffeeNChill.Gateway.Services;

/// <summary>
/// Producer side of the order workflow: puts orders on order-processing-queue (the ProcessOrderQueue
/// Function picks them up) and reads/updates the Orders table that Function writes to.
/// </summary>
public sealed class OrderService : TableBackedService, IOrderService
{
    private const string QueueName = "order-processing-queue";

    private readonly QueueClient _queue;
    private readonly ILogger<OrderService> _logger;
    private volatile bool _queueReady;

    public OrderService(IConfiguration config, ILogger<OrderService> logger)
        : base(new TableClient(StorageSettings.Primary(config), "Orders"))
    {
        _logger = logger;

        // host.json in the Functions project sets messageEncoding = base64, so the producer must encode too.
        _queue = new QueueClient(
            StorageSettings.Primary(config),
            QueueName,
            new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
    }

    public async Task EnqueueAsync(QueuedOrder order, CancellationToken ct = default)
    {
        try
        {
            if (!_queueReady)
            {
                await _queue.CreateIfNotExistsAsync(cancellationToken: ct);
                _queueReady = true;
            }

            // PascalCase JSON, matching the payload in the assignment brief.
            string json = JsonSerializer.Serialize(order);
            await _queue.SendMessageAsync(json, cancellationToken: ct);

            _logger.LogInformation("Order {OrderId} placed on {Queue}.", order.OrderId, QueueName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not place order {OrderId} on {Queue}.", order.OrderId, QueueName);
            throw new ServiceUnavailableException(
                "The order queue is currently unavailable. Please try again shortly.", ex);
        }
    }

    public async Task<OrderEntity?> GetAsync(string orderId, CancellationToken ct = default)
    {
        var table = await ReadyTableAsync(ct);

        // The PartitionKey is the order date, which callers don't know, so filter on RowKey (OrderId).
        string filter = TableClient.CreateQueryFilter($"RowKey eq {orderId}");

        await foreach (var order in table.QueryAsync<OrderEntity>(filter, maxPerPage: 1, cancellationToken: ct))
            return order;

        return null;
    }

    public async Task<OrderEntity?> UpdateStatusAsync(string orderId, string status, CancellationToken ct = default)
    {
        if (!OrderStatuses.TryNormalise(status, out string newStatus))
            throw new ApiValidationException($"Status must be one of: {string.Join(", ", OrderStatuses.All)}.");

        OrderEntity? order = await GetAsync(orderId, ct);
        if (order is null) return null;

        // Statuses only move forward: Received → Preparing → Ready → Collected.
        if (OrderStatuses.Rank(newStatus) <= OrderStatuses.Rank(order.Status))
            throw new ConflictException($"Order '{orderId}' is already '{order.Status}'; it cannot move to '{newStatus}'.");

        order.Status = newStatus;
        order.LastUpdated = DateTimeOffset.UtcNow;

        var table = await ReadyTableAsync(ct);
        await table.UpdateEntityAsync(order, order.ETag, TableUpdateMode.Replace, ct);

        _logger.LogInformation("Order {OrderId} status set to {Status} by staff.", orderId, newStatus);
        return order;
    }
}
