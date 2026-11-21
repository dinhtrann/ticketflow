using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Seats;

public sealed record SeatReleaseResponse(
    Guid SeatId,
    SeatStatus Status,
    DateTimeOffset ReleasedAtUtc);
