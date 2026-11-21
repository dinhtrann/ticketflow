namespace TicketFlow.Domain.Exceptions;

/// <summary>
/// Ngoại lệ gốc cho mọi lỗi nghiệp vụ/domain. <see cref="Code"/> tương ứng
/// với mã lỗi trong danh mục API_SPEC §1.3.
/// </summary>
public abstract class TicketFlowException : Exception
{
    protected TicketFlowException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
