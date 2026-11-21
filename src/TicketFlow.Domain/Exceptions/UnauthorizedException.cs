namespace TicketFlow.Domain.Exceptions;

/// <summary>
/// Thiếu xác thực hoặc token không hợp lệ — HTTP 401.
/// </summary>
public class UnauthorizedException : TicketFlowException
{
    public UnauthorizedException(string code = "UNAUTHORIZED", string message = "Authentication is required.")
        : base(code, message)
    {
    }
}
