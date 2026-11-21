using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Events;
using TicketFlow.Application.Dtos.Seats;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;

namespace TicketFlow.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class SeatsController(ISeatService seatService) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    [HttpGet("events/{eventId:guid}/seats")]
    public async Task<ActionResult<PagedResult<SeatDto>>> GetSeats(
        Guid eventId,
        [FromQuery] SeatSearchQuery query,
        CancellationToken ct)
    {
        var result = await seatService.GetSeatsAsync(eventId, query, IsAdmin, ct);
        return Ok(result);
    }

    [HttpPost("seats/{seatId:guid}/hold")]
    [Authorize]
    public async Task<ActionResult<SeatHoldResponse>> Hold(Guid seatId, CancellationToken ct)
    {
        var result = await seatService.HoldAsync(seatId, GetCurrentUserId(), ct);
        return Ok(result);
    }

    [HttpPost("seats/{seatId:guid}/release")]
    [Authorize]
    public async Task<ActionResult<SeatReleaseResponse>> Release(Guid seatId, CancellationToken ct)
    {
        var result = await seatService.ReleaseAsync(seatId, GetCurrentUserId(), IsAdmin, ct);
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
