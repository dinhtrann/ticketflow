using TicketFlow.Application.Common;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Interfaces;

public interface ISeatRepository
{
    /// <summary>AsNoTracking — compose filter/sort/paging/Select ở Application.</summary>
    IQueryable<Seat> GetQueryable();
    /// <summary>Tracking + Include(Event) — dùng cho hold/release/confirm write path.</summary>
    Task<Seat?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default);
    void AddRange(IEnumerable<Seat> seats);
    Task<SeatStatusCounts> GetStatusCountsAsync(Guid eventId, DateTimeOffset utcNow, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, SeatStatusCounts>> GetStatusCountsAsync(
        IReadOnlyCollection<Guid> eventIds, DateTimeOffset utcNow, CancellationToken ct = default);
}
