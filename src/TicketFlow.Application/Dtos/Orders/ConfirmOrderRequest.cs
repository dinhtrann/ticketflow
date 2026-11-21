namespace TicketFlow.Application.Dtos.Orders;

public sealed record ConfirmOrderRequest
{
    public Guid SeatId { get; init; }
}
