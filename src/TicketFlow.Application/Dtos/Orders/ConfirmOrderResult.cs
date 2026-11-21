namespace TicketFlow.Application.Dtos.Orders;

/// <summary>Kết quả confirm: <see cref="IsNew"/> = true → 201 Created; false → 200 OK (idempotent replay).</summary>
public sealed record ConfirmOrderResult(OrderDto Order, bool IsNew);
