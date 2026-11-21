namespace TicketFlow.Application.Options;

public class BookingOptions
{
    public int HoldDurationMinutes { get; set; } = 10;
    public int ReleaseWorkerIntervalSeconds { get; set; } = 30;
}
