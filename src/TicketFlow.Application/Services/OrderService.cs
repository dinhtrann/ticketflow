using System.Security.Cryptography;
using System.Text;
using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Orders;
using TicketFlow.Application.Interfaces;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;

namespace TicketFlow.Application.Services;

public class OrderService(
    IOrderRepository orderRepository,
    ISeatRepository seatRepository,
    IUnitOfWork unitOfWork,
    IAsyncQueryExecutor queryExecutor,
    TimeProvider timeProvider) : IOrderService
{
    public async Task<ConfirmOrderResult> ConfirmAsync(
        Guid seatId,
        string? idempotencyKey,
        Guid userId,
        CancellationToken ct = default)
    {
        EnsureValidIdempotencyKey(idempotencyKey);
        var key = idempotencyKey!;
        var requestHash = ComputeRequestHash(seatId);

        var existing = await orderRepository.FindByUserAndIdempotencyKeyAsync(userId, key, ct);
        if (existing is not null)
        {
            return ResolveExisting(existing, requestHash);
        }

        var seat = await seatRepository.GetByIdTrackedAsync(seatId, ct)
            ?? throw new NotFoundException("SEAT_NOT_FOUND", "Seat not found.");

        var now = timeProvider.GetUtcNow();
        EnsureEventBookable(seat.Event, now);

        if (seat.Status == SeatStatus.Sold)
        {
            throw new DomainValidationException("SEAT_ALREADY_SOLD", "Seat is already sold.");
        }

        if (seat.Status == SeatStatus.Held && seat.HeldUntilUtc <= now)
        {
            throw new DomainValidationException("SEAT_HOLD_EXPIRED", "Seat hold has expired.");
        }

        if (seat.Status != SeatStatus.Held || seat.HeldByUserId != userId)
        {
            throw new DomainValidationException("SEAT_NOT_HELD_BY_USER", "Seat is not held by the current user.");
        }

        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            EventId = seat.EventId,
            SeatId = seat.Id,
            TotalAmount = seat.Price,
            Status = OrderStatus.Confirmed,
            IdempotencyKey = key,
            RequestHash = requestHash,
            CreatedAtUtc = now,
        };

        seat.Status = SeatStatus.Sold;
        seat.HeldUntilUtc = null;

        orderRepository.Add(order);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is UniqueConstraintViolationException or ConcurrencyConflictException)
        {
            return await ResolveAfterConflictAsync(userId, key, requestHash, ct);
        }

        return new ConfirmOrderResult(
            new OrderDto(
                order.Id,
                order.UserId,
                order.EventId,
                seat.Event.Title,
                order.SeatId,
                seat.SeatCode,
                seat.Section,
                order.TotalAmount,
                order.Status,
                order.IdempotencyKey,
                order.CreatedAtUtc),
            IsNew: true);
    }

    public async Task<PagedResult<OrderDto>> GetMyOrdersAsync(
        Guid userId,
        OrderSearchQuery query,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var orders = orderRepository.GetQueryable()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenByDescending(o => o.Id);

        var totalCount = await queryExecutor.CountAsync(orders, ct);
        var projected = orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderDto(
                o.Id,
                o.UserId,
                o.EventId,
                o.Event.Title,
                o.SeatId,
                o.Seat.SeatCode,
                o.Seat.Section,
                o.TotalAmount,
                o.Status,
                o.IdempotencyKey,
                o.CreatedAtUtc));

        var items = await queryExecutor.ToListAsync(projected, ct);
        return new PagedResult<OrderDto>(items, page, pageSize, totalCount);
    }

    public async Task<OrderDto> GetByIdAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var order = await orderRepository.GetByIdAsync(orderId, ct)
            ?? throw new NotFoundException("ORDER_NOT_FOUND", "Order not found.");

        if (!isAdmin && order.UserId != userId)
        {
            throw new ForbiddenException();
        }

        return OrderDto.FromOrder(order);
    }

    private async Task<ConfirmOrderResult> ResolveAfterConflictAsync(
        Guid userId,
        string idempotencyKey,
        string requestHash,
        CancellationToken ct)
    {
        var existing = await orderRepository.FindByUserAndIdempotencyKeyAsync(userId, idempotencyKey, ct);
        if (existing is null)
        {
            throw new ConcurrencyConflictException();
        }

        return ResolveExisting(existing, requestHash);
    }

    private static ConfirmOrderResult ResolveExisting(Order existing, string requestHash)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException(
                "IDEMPOTENCY_KEY_PAYLOAD_MISMATCH",
                "Idempotency-Key was already used with a different payload.");
        }

        return new ConfirmOrderResult(OrderDto.FromOrder(existing), IsNew: false);
    }

    private static string ComputeRequestHash(Guid seatId)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes($"{seatId:D}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void EnsureValidIdempotencyKey(string? key)
    {
        var isValid = key is { Length: >= 8 and <= 128 }
            && key.All(c => c is >= '!' and <= '~');

        if (!isValid)
        {
            throw new DomainValidationException(
                "VALIDATION_ERROR",
                "Idempotency-Key header is required and must be 8-128 ASCII characters without spaces.");
        }
    }

    private static void EnsureEventBookable(Event ev, DateTimeOffset utcNow)
    {
        if (ev.Status != EventStatus.Published || ev.StartsAtUtc <= utcNow)
        {
            throw new DomainValidationException(
                "EVENT_NOT_BOOKABLE",
                "Event is not open for booking.");
        }
    }
}
