namespace TicketFlow.Application.Dtos.Events;

public sealed record BulkSeatResponse(
    Guid EventId,
    int CreatedCount,
    IReadOnlyList<SeatDto> Seats);
