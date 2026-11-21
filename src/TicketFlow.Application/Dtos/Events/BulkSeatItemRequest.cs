namespace TicketFlow.Application.Dtos.Events;

public sealed record BulkSeatItemRequest
{
    public string SeatCode { get; init; } = string.Empty;
    public string Section { get; init; } = string.Empty;
    public decimal Price { get; init; }
}
