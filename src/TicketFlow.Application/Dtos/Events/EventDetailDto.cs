using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Events;

public sealed record EventDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string Venue,
    DateTimeOffset StartsAtUtc,
    EventStatus Status,
    int TotalSeats,
    int AvailableSeats,
    int HeldSeats,
    int SoldSeats,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
