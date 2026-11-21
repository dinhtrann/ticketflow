namespace TicketFlow.Application.Dtos.Events;

public sealed record BulkSeatRequest
{
    public IReadOnlyList<BulkSeatItemRequest> Seats { get; init; } = [];
}
