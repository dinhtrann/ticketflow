namespace TicketFlow.Application.Interfaces;

public interface IUnitOfWork
{
    /// <summary>
    /// Lưu thay đổi trong một transaction. Implementation PHẢI translate:
    /// DbUpdateConcurrencyException → ConcurrencyConflictException;
    /// DbUpdateException chứa SqlException 2601/2627 → UniqueConstraintViolationException.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
