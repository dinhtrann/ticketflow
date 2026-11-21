using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Events;

public sealed record EventSearchQuery
{
    public string? Search { get; init; }
    public EventStatus? Status { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public string? SortBy { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
