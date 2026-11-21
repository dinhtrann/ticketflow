using TicketFlow.Application.Common;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Interfaces;

public interface ISeatRepository
{
    /// <summary>AsNoTracking — compose filter/sort/paging/Select ở Application.</summary>
    IQueryable<Seat> GetQueryable();
    /// <summary>Tracking + Include(Event) — dùng cho hold/release/confirm write path.</summary>
    Task<Seat?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default);
    /// <summary>Tracking — lấy ghế Held của event để cancel event giải phóng trong cùng SaveChanges.</summary>
    Task<IReadOnlyList<Seat>> GetHeldSeatsTrackedAsync(Guid eventId, CancellationToken ct = default);
    void AddRange(IEnumerable<Seat> seats);
    /// <summary>AsNoTracking — kiểm tra SeatCode đã tồn tại trong event trước khi bulk insert.</summary>
    Task<IReadOnlyCollection<string>> GetExistingSeatCodesAsync(Guid eventId, IReadOnlyCollection<string> seatCodes, CancellationToken ct = default);
    Task<SeatStatusCounts> GetStatusCountsAsync(Guid eventId, DateTimeOffset utcNow, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, SeatStatusCounts>> GetStatusCountsAsync(
        IReadOnlyCollection<Guid> eventIds, DateTimeOffset utcNow, CancellationToken ct = default);
}
