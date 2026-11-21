using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Events;

public sealed record UpdateEventStatusRequest
{
    public EventStatus Status { get; init; }
}
