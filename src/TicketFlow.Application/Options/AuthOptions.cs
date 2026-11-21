namespace TicketFlow.Application.Options;

public class AuthOptions
{
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
}
