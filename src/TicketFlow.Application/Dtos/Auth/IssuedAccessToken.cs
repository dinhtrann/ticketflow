namespace TicketFlow.Application.Dtos.Auth;

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);
