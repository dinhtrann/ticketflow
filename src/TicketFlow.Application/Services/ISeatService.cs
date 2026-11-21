using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Events;
using TicketFlow.Application.Dtos.Seats;

namespace TicketFlow.Application.Services;

public interface ISeatService
{
    /// <summary>Giữ chỗ ghế 10 phút (cấu hình Booking:HoldDurationMinutes), concurrency qua RowVersion.</summary>
    Task<SeatHoldResponse> HoldAsync(Guid seatId, Guid userId, CancellationToken ct = default);

    /// <summary>Hủy giữ chỗ sớm — chỉ chủ hold hoặc Admin.</summary>
    Task<SeatReleaseResponse> ReleaseAsync(Guid seatId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Danh sách ghế của event có lọc section/status + phân trang (anonymous được phép với event Published).</summary>
    Task<PagedResult<SeatDto>> GetSeatsAsync(Guid eventId, SeatSearchQuery query, bool isAdmin, CancellationToken ct = default);
}
