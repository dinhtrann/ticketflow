using TicketFlow.Application.Interfaces;

namespace TicketFlow.UnitTests.Helpers;

/// <summary>
/// Fake <see cref="IAsyncQueryExecutor"/> materialize IQueryable đồng bộ trên LINQ-to-Objects.
/// Chỉ dùng khi repository mock trả <c>list.AsQueryable()</c> — đủ cho Where/Select/Skip/Take/OrderBy.
/// </summary>
public sealed class InMemoryQueryExecutor : IAsyncQueryExecutor
{
    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        => Task.FromResult(query.Count());

    public Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(query.ToList());

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default)
        => Task.FromResult(query.FirstOrDefault());
}
