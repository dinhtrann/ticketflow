using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Interfaces;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Repositories;

public class EventRepository(AppDbContext db) : IEventRepository
{
    public IQueryable<Event> GetQueryable() => db.Events.AsNoTracking();

    public Task<Event?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default)
        => db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);

    public void Add(Event ev) => db.Events.Add(ev);
}
