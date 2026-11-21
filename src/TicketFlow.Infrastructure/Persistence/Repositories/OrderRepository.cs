using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Interfaces;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class OrderRepository(AppDbContext db) : IOrderRepository
{
    public IQueryable<Order> GetQueryable() => db.Orders.AsNoTracking();

    public Task<Order?> FindByUserAndIdempotencyKeyAsync(Guid userId, string idempotencyKey, CancellationToken ct = default)
        => db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.UserId == userId && o.IdempotencyKey == idempotencyKey, ct);

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

    public void Add(Order order) => db.Orders.Add(order);
}
