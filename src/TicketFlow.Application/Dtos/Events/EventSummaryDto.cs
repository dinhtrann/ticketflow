using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Events;

public sealed record EventSummaryDto(
    Guid Id,
    string Title,
    string Venue,
    DateTimeOffset StartsAtUtc,
    EventStatus Status,
    int TotalSeats,
    int AvailableSeats);
