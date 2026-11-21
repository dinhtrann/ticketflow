namespace TicketFlow.Application.Dtos.Auth;

public sealed record RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
