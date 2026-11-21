using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Common;
using TicketFlow.Application.Interfaces;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class SeatRepository(AppDbContext db) : ISeatRepository
{
    public IQueryable<Seat> GetQueryable() => db.Seats.AsNoTracking();

    public Task<Seat?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default)
        => db.Seats.Include(s => s.Event).FirstOrDefaultAsync(s => s.Id == id, ct);

    public void AddRange(IEnumerable<Seat> seats) => db.Seats.AddRange(seats);

    public async Task<SeatStatusCounts> GetStatusCountsAsync(Guid eventId, DateTimeOffset utcNow, CancellationToken ct = default)
    {
        var rows = await db.Seats.AsNoTracking()
            .Where(s => s.EventId == eventId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Sold = g.Count(s => s.Status == SeatStatus.Sold),
                HeldActive = g.Count(s => s.Status == SeatStatus.Held && s.HeldUntilUtc > utcNow),
            })
            .FirstAsync(ct);

        return ToCounts(rows.Total, rows.Sold, rows.HeldActive);
    }

    public async Task<IReadOnlyDictionary<Guid, SeatStatusCounts>> GetStatusCountsAsync(
        IReadOnlyCollection<Guid> eventIds, DateTimeOffset utcNow, CancellationToken ct = default)
    {
        var rows = await db.Seats.AsNoTracking()
            .Where(s => eventIds.Contains(s.EventId))
            .GroupBy(s => s.EventId)
            .Select(g => new
            {
                EventId = g.Key,
                Total = g.Count(),
                Sold = g.Count(s => s.Status == SeatStatus.Sold),
                HeldActive = g.Count(s => s.Status == SeatStatus.Held && s.HeldUntilUtc > utcNow),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.EventId,
            r => ToCounts(r.Total, r.Sold, r.HeldActive));
    }

    private static SeatStatusCounts ToCounts(int total, int sold, int heldActive)
        => new(total, total - sold - heldActive, heldActive, sold);
}
