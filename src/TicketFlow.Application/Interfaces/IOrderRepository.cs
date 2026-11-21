using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Interfaces;

public interface IOrderRepository
{
    /// <summary>AsNoTracking — compose filter/paging/Select sang DTO ở Application.</summary>
    IQueryable<Order> GetQueryable();
    /// <summary>AsNoTracking — tra cứu idempotency theo (UserId, IdempotencyKey).</summary>
    Task<Order?> FindByUserAndIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken ct = default);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Order order);
}
