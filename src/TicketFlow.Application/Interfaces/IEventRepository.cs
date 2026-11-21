using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Interfaces;

public interface IEventRepository
{
    /// <summary>AsNoTracking — compose filter/sort/paging/Select ở Application.</summary>
    IQueryable<Event> GetQueryable();
    /// <summary>AsNoTracking — đọc một event cho path không cần tracking.</summary>
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Tracking — dùng cho write path (đổi Status, ...).</summary>
    Task<Event?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default);
    void Add(Event ev);
}
