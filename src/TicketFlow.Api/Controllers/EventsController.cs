using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Events;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Api.Controllers;

[ApiController]
[Route("api/v1/events")]
public class EventsController(IEventService eventService) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    [HttpGet]
    public async Task<ActionResult<PagedResult<EventSummaryDto>>> Search(
        [FromQuery] EventSearchQuery query,
        CancellationToken ct)
    {
        var result = await eventService.SearchAsync(query, IsAdmin, ct);
        return Ok(result);
    }

    [HttpGet("{eventId:guid}")]
    public async Task<ActionResult<EventDetailDto>> GetById(Guid eventId, CancellationToken ct)
    {
        var result = await eventService.GetByIdAsync(eventId, IsAdmin, ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<EventDetailDto>> Create(CreateEventRequest request, CancellationToken ct)
    {
        var result = await eventService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { eventId = result.Id }, result);
    }

    [HttpPatch("{eventId:guid}/status")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<EventDetailDto>> UpdateStatus(
        Guid eventId,
        UpdateEventStatusRequest request,
        CancellationToken ct)
    {
        var result = await eventService.UpdateStatusAsync(eventId, request, ct);
        return Ok(result);
    }

    [HttpPost("{eventId:guid}/seats/bulk")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<BulkSeatResponse>> BulkCreateSeats(
        Guid eventId,
        BulkSeatRequest request,
        CancellationToken ct)
    {
        var result = await eventService.BulkCreateSeatsAsync(eventId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
