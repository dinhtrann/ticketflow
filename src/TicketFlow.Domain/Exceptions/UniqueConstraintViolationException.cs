namespace TicketFlow.Domain.Exceptions;

/// <summary>
/// Vi phạm ràng buộc UNIQUE/CHECK ở tầng CSDL (SqlException 2601/2627) — HTTP 409.
/// </summary>
public class UniqueConstraintViolationException : TicketFlowException
{
    public UniqueConstraintViolationException(string constraintName, string message)
        : base("DUPLICATE_SEAT_CODE", message)
    {
        ConstraintName = constraintName;
    }

    public string ConstraintName { get; }
}
