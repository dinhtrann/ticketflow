namespace TicketFlow.Application.Dtos.Auth;

public sealed record RevokeRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
