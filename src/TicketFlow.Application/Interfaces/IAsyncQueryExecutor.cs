namespace TicketFlow.Application.Interfaces;

/// <summary>
/// Thực thi IQueryable bất đồng bộ mà không để Application tham chiếu EF Core.
/// Implementation ở Infrastructure dùng EF Core async operators.
/// </summary>
public interface IAsyncQueryExecutor
{
    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default);
}
