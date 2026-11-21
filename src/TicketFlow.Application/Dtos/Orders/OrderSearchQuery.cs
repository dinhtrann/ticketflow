namespace TicketFlow.Application.Dtos.Orders;

public sealed record OrderSearchQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
