using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Seats;

public sealed record SeatHoldResponse(
    Guid SeatId,
    Guid EventId,
    string SeatCode,
    decimal Price,
    SeatStatus Status,
    Guid HeldByUserId,
    DateTimeOffset HeldUntilUtc,
    string RowVersion);
