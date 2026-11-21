using TicketFlow.Domain.Enums;

namespace TicketFlow.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid EventId { get; set; }
    public Guid SeatId { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Event Event { get; set; } = null!;
    public Seat Seat { get; set; } = null!;
}
