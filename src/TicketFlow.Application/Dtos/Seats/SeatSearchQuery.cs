using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Seats;

public sealed record SeatSearchQuery
{
    public string? Section { get; init; }
    public SeatStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
