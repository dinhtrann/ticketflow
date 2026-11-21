namespace TicketFlow.Application.Common;

/// <summary>
/// Số liệu trạng thái ghế của một sự kiện tại thời điểm <c>utcNow</c>.
/// <see cref="AvailableSeats"/> = ghế đặt được ngay (Available + Held đã quá hạn,
/// theo đúng quy tắc API_SPEC §3.6). <see cref="HeldSeats"/> chỉ tính hold còn hiệu lực.
/// </summary>
public sealed record SeatStatusCounts(
    int TotalSeats,
    int AvailableSeats,
    int HeldSeats,
    int SoldSeats);
