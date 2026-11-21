using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketFlow.Api.RateLimiting;
using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Orders;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;

namespace TicketFlow.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost("confirm")]
    [EnableRateLimiting(RateLimitPolicies.Booking)]
    public async Task<ActionResult<OrderDto>> Confirm(ConfirmOrderRequest request, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var result = await orderService.ConfirmAsync(request.SeatId, idempotencyKey, GetCurrentUserId(), ct);

        if (result.IsNew)
        {
            return CreatedAtAction(nameof(GetById), new { orderId = result.Order.OrderId }, result.Order);
        }

        return Ok(result.Order);
    }

    [HttpGet("me")]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetMyOrders(
        [FromQuery] OrderSearchQuery query,
        CancellationToken ct)
    {
        var result = await orderService.GetMyOrdersAsync(GetCurrentUserId(), query, ct);
        return Ok(result);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid orderId, CancellationToken ct)
    {
        var result = await orderService.GetByIdAsync(
            orderId,
            GetCurrentUserId(),
            User.IsInRole(nameof(UserRole.Admin)),
            ct);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var subject = User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
        {
            throw new UnauthorizedException();
        }

        return userId;
    }
}
