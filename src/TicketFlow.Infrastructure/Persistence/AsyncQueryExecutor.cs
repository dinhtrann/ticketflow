using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Interfaces;

namespace TicketFlow.Infrastructure.Persistence;

public sealed class AsyncQueryExecutor : IAsyncQueryExecutor
{
    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        => query.CountAsync(ct);

    public async Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        => await query.ToListAsync(ct);

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        => query.FirstOrDefaultAsync(ct);
}
