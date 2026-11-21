namespace TicketFlow.Application.Dtos.Events;

public sealed record CreateEventRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Venue { get; init; } = string.Empty;
    public DateTimeOffset StartsAtUtc { get; init; }
}
