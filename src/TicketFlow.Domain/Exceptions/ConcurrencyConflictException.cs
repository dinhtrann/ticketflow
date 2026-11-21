namespace TicketFlow.Domain.Exceptions;

/// <summary>
/// Xung đột cập nhật đồng thời (RowVersion thay đổi) — HTTP 409.
/// </summary>
public class ConcurrencyConflictException : TicketFlowException
{
    public ConcurrencyConflictException(string message = "The resource was modified by another request.")
        : base("SEAT_CONCURRENCY_CONFLICT", message)
    {
    }
}
