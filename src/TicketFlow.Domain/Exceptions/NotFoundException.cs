namespace TicketFlow.Domain.Exceptions;

/// <summary>
/// Không tìm thấy tài nguyên — HTTP 404. Truyền <paramref name="code"/>
/// theo catalog (EVENT_NOT_FOUND, SEAT_NOT_FOUND, ORDER_NOT_FOUND).
/// </summary>
public class NotFoundException : TicketFlowException
{
    public NotFoundException(string code, string message)
        : base(code, message)
    {
    }
}
