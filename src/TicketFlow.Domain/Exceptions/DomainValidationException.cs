namespace TicketFlow.Domain.Exceptions;

/// <summary>
/// Dữ liệu nghiệp vụ không hợp lệ (validation / state machine) — HTTP 400/409.
/// </summary>
public class DomainValidationException : TicketFlowException
{
    public DomainValidationException(string code, string message)
        : base(code, message)
    {
    }
}
