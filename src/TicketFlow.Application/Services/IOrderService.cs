using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Orders;

namespace TicketFlow.Application.Services;

public interface IOrderService
{
    /// <summary>
    /// Xác nhận đơn & chuyển ghế Held → Sold. Idempotent theo (UserId, IdempotencyKey):
    /// cùng key + cùng payload trả order cũ (IsNew=false), khác payload → 422.
    /// </summary>
    Task<ConfirmOrderResult> ConfirmAsync(Guid seatId, string? idempotencyKey, Guid userId, CancellationToken ct = default);

    /// <summary>Lịch sử đơn của user, sắp xếp createdAtUtc DESC, có phân trang.</summary>
    Task<PagedResult<OrderDto>> GetMyOrdersAsync(Guid userId, OrderSearchQuery query, CancellationToken ct = default);

    /// <summary>Chi tiết đơn — chỉ owner hoặc Admin.</summary>
    Task<OrderDto> GetByIdAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct = default);
}
