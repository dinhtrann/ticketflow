using TicketFlow.Domain.Enums;

namespace TicketFlow.Domain.Entities;

public class Seat
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string SeatCode { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public SeatStatus Status { get; set; }
    public Guid? HeldByUserId { get; set; }
    public DateTimeOffset? HeldUntilUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Event Event { get; set; } = null!;
    public User? HeldByUser { get; set; }
    public Order? Order { get; set; }
}
