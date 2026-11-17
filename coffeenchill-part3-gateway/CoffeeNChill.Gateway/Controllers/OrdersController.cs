using CoffeeNChill.Gateway.Auth;
using CoffeeNChill.Gateway.DTOs;
using CoffeeNChill.Gateway.Exceptions;
using CoffeeNChill.Gateway.Mapping;
using CoffeeNChill.Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

/// <summary>Order placement (public) and order status tracking.</summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    /// <summary>
    /// Places an order on the queue. The request returns immediately; the ProcessOrderQueue Function
    /// creates the Orders table row and advances its status in the background.
    /// </summary>
    /// <response code="202">Order accepted. Poll GET /api/orders/{orderId} for progress.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="503">The queue is unreachable.</response>
    [HttpPost("queue")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(QueuedOrderResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<QueuedOrderResponseDto>> QueueOrder([FromBody] CreateOrderDto request, CancellationToken ct)
    {
        var order = request.ToQueuedOrder(DateTimeOffset.UtcNow);
        await _orders.EnqueueAsync(order, ct);

        return AcceptedAtAction(nameof(GetOrder), new { orderId = order.OrderId }, order.ToResponse());
    }

    /// <summary>Gets the current status of an order.</summary>
    /// <param name="orderId">The order id returned when it was queued.</param>
    /// <response code="200">The order and its status.</response>
    /// <response code="404">Not found. It may still be waiting in the queue.</response>
    [HttpGet("{orderId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OrderStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderStatusDto>> GetOrder(string orderId, CancellationToken ct)
    {
        var order = await _orders.GetAsync(orderId.Trim(), ct)
                    ?? throw new NotFoundException($"Order '{orderId}' was not found. It may still be waiting in the queue.");
        return Ok(order.ToDto());
    }

    /// <summary>Advances an order's status (Admin or Staff). Statuses only move forward.</summary>
    /// <response code="200">The updated order.</response>
    /// <response code="400">Unknown status.</response>
    /// <response code="401">Missing or invalid token.</response>
    /// <response code="403">Token is valid but the role is not allowed.</response>
    /// <response code="404">No such order.</response>
    /// <response code="409">The status would not move forward.</response>
    [HttpPut("status")]
    [Authorize(Roles = AppRoles.AdminOrStaff)]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OrderStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderStatusDto>> UpdateStatus([FromBody] UpdateOrderStatusDto request, CancellationToken ct)
    {
        var updated = await _orders.UpdateStatusAsync(request.OrderId.Trim(), request.Status, ct)
                      ?? throw new NotFoundException($"Order '{request.OrderId}' was not found.");
        return Ok(updated.ToDto());
    }
}
