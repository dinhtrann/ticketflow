using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Options;
using TicketFlow.Domain.Enums;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Workers;

public sealed class ExpiredSeatReleaseWorker(
    IServiceScopeFactory scopeFactory,
    BookingOptions bookingOptions,
    TimeProvider timeProvider,
    ILogger<ExpiredSeatReleaseWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(1, bookingOptions.ReleaseWorkerIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        do
        {
            try
            {
                await ReleaseExpiredHoldsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Expired seat release cycle failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ReleaseExpiredHoldsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var utcNow = timeProvider.GetUtcNow();

        var released = await db.Seats
            .Where(s => s.Status == SeatStatus.Held && s.HeldUntilUtc <= utcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SeatStatus.Available)
                .SetProperty(x => x.HeldByUserId, (Guid?)null)
                .SetProperty(x => x.HeldUntilUtc, (DateTimeOffset?)null), ct);

        if (released > 0)
        {
            logger.LogInformation("Released {Count} expired seat hold(s).", released);
        }
    }
}
