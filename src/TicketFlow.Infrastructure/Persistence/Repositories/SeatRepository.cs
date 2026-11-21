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

    public async Task<IReadOnlyList<Seat>> GetHeldSeatsTrackedAsync(Guid eventId, CancellationToken ct = default)
    {
        var seats = await db.Seats
            .Where(s => s.EventId == eventId && s.Status == SeatStatus.Held)
            .ToListAsync(ct);
        return seats;
    }

    public void AddRange(IEnumerable<Seat> seats) => db.Seats.AddRange(seats);

    public async Task<IReadOnlyCollection<string>> GetExistingSeatCodesAsync(
        Guid eventId,
        IReadOnlyCollection<string> seatCodes,
        CancellationToken ct = default)
    {
        var codes = await db.Seats.AsNoTracking()
            .Where(s => s.EventId == eventId && seatCodes.Contains(s.SeatCode))
            .Select(s => s.SeatCode)
            .ToListAsync(ct);
        return codes;
    }

    public async Task<SeatStatusCounts> GetStatusCountsAsync(Guid eventId, DateTimeOffset utcNow, CancellationToken ct = default)
    {
        var counts = await GetStatusCountsAsync(new[] { eventId }, utcNow, ct);
        return counts.TryGetValue(eventId, out var result) ? result : new SeatStatusCounts(0, 0, 0, 0);
    }

    public async Task<IReadOnlyDictionary<Guid, SeatStatusCounts>> GetStatusCountsAsync(
        IReadOnlyCollection<Guid> eventIds,
        DateTimeOffset utcNow,
        CancellationToken ct = default)
    {
        if (eventIds.Count == 0)
        {
            return new Dictionary<Guid, SeatStatusCounts>();
        }

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
