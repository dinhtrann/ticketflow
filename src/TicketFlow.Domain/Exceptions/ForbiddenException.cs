namespace TicketFlow.Domain.Exceptions;

/// <summary>
/// Đã xác thực nhưng thiếu quyền / không phải chủ sở hữu — HTTP 403.
/// </summary>
public class ForbiddenException : TicketFlowException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base("FORBIDDEN", message)
    {
    }
}
