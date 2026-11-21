using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Events;

public sealed record SeatDto(
    Guid Id,
    Guid EventId,
    string SeatCode,
    string Section,
    decimal Price,
    SeatStatus Status,
    DateTimeOffset? HeldUntilUtc)
{
    public static SeatDto FromSeat(Seat seat, DateTimeOffset utcNow)
    {
        var isExpiredHold = seat.Status == SeatStatus.Held
            && seat.HeldUntilUtc is { } heldUntil
            && heldUntil <= utcNow;

        return new SeatDto(
            seat.Id,
            seat.EventId,
            seat.SeatCode,
            seat.Section,
            seat.Price,
            isExpiredHold ? SeatStatus.Available : seat.Status,
            isExpiredHold ? null : seat.HeldUntilUtc);
    }
}
