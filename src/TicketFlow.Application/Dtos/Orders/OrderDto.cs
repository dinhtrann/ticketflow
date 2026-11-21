using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Orders;

public sealed record OrderDto(
    Guid OrderId,
    Guid UserId,
    Guid EventId,
    string EventTitle,
    Guid SeatId,
    string SeatCode,
    string Section,
    decimal TotalAmount,
    OrderStatus Status,
    string IdempotencyKey,
    DateTimeOffset CreatedAtUtc)
{
    public static OrderDto FromOrder(Order order) =>
        new(
            order.Id,
            order.UserId,
            order.EventId,
            order.Event.Title,
            order.SeatId,
            order.Seat.SeatCode,
            order.Seat.Section,
            order.TotalAmount,
            order.Status,
            order.IdempotencyKey,
            order.CreatedAtUtc);
}
