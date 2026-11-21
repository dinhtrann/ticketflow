using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Events;
using TicketFlow.Application.Dtos.Seats;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Options;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;

namespace TicketFlow.Application.Services;

public class SeatService(
    ISeatRepository seatRepository,
    IEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    IAsyncQueryExecutor queryExecutor,
    TimeProvider timeProvider,
    BookingOptions bookingOptions) : ISeatService
{
    public async Task<SeatHoldResponse> HoldAsync(Guid seatId, Guid userId, CancellationToken ct = default)
    {
        var seat = await seatRepository.GetByIdTrackedAsync(seatId, ct)
            ?? throw new NotFoundException("SEAT_NOT_FOUND", "Seat not found.");

        var now = timeProvider.GetUtcNow();
        EnsureEventBookable(seat.Event, now);

        if (seat.Status == SeatStatus.Sold)
        {
            throw new DomainValidationException("SEAT_ALREADY_SOLD", "Seat is already sold.");
        }

        if (seat.Status == SeatStatus.Held && seat.HeldUntilUtc > now)
        {
            throw new DomainValidationException("SEAT_ALREADY_HELD", "Seat is currently held by another request.");
        }

        seat.Status = SeatStatus.Held;
        seat.HeldByUserId = userId;
        seat.HeldUntilUtc = now + TimeSpan.FromMinutes(bookingOptions.HoldDurationMinutes);

        await unitOfWork.SaveChangesAsync(ct);

        return new SeatHoldResponse(
            seat.Id,
            seat.EventId,
            seat.SeatCode,
            seat.Price,
            seat.Status,
            seat.HeldByUserId!.Value,
            seat.HeldUntilUtc!.Value,
            Convert.ToBase64String(seat.RowVersion));
    }

    public async Task<SeatReleaseResponse> ReleaseAsync(Guid seatId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var seat = await seatRepository.GetByIdTrackedAsync(seatId, ct)
            ?? throw new NotFoundException("SEAT_NOT_FOUND", "Seat not found.");

        if (seat.Status != SeatStatus.Held)
        {
            throw new DomainValidationException("SEAT_NOT_HELD", "Seat is not in Held status.");
        }

        if (!isAdmin && seat.HeldByUserId != userId)
        {
            throw new ForbiddenException();
        }

        seat.Status = SeatStatus.Available;
        seat.HeldByUserId = null;
        seat.HeldUntilUtc = null;

        var releasedAtUtc = timeProvider.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);

        return new SeatReleaseResponse(seat.Id, seat.Status, releasedAtUtc);
    }

    public async Task<PagedResult<SeatDto>> GetSeatsAsync(
        Guid eventId,
        SeatSearchQuery query,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var ev = await eventRepository.GetByIdAsync(eventId, ct)
            ?? throw new NotFoundException("EVENT_NOT_FOUND", "Event not found.");

        if (!isAdmin && ev.Status != EventStatus.Published)
        {
            throw new NotFoundException("EVENT_NOT_FOUND", "Event not found.");
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var utcNow = timeProvider.GetUtcNow();

        var seats = seatRepository.GetQueryable().Where(s => s.EventId == eventId);

        if (!string.IsNullOrWhiteSpace(query.Section))
        {
            var section = query.Section.Trim();
            seats = seats.Where(s => s.Section == section);
        }

        if (query.Status is { } status)
        {
            seats = status switch
            {
                SeatStatus.Available => seats.Where(s =>
                    s.Status == SeatStatus.Available
                    || (s.Status == SeatStatus.Held && s.HeldUntilUtc <= utcNow)),
                SeatStatus.Held => seats.Where(s =>
                    s.Status == SeatStatus.Held && s.HeldUntilUtc > utcNow),
                SeatStatus.Sold => seats.Where(s => s.Status == SeatStatus.Sold),
                _ => seats,
            };
        }

        seats = seats.OrderBy(s => s.Section).ThenBy(s => s.SeatCode);

        var totalCount = await queryExecutor.CountAsync(seats, ct);
        var projected = seats
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SeatDto(
                s.Id,
                s.EventId,
                s.SeatCode,
                s.Section,
                s.Price,
                s.Status,
                s.HeldUntilUtc));

        var rawItems = await queryExecutor.ToListAsync(projected, ct);
        var items = rawItems
            .Select(dto => ToEffectiveStatus(dto, utcNow))
            .ToList();

        return new PagedResult<SeatDto>(items, page, pageSize, totalCount);
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

    private static SeatDto ToEffectiveStatus(SeatDto dto, DateTimeOffset utcNow)
    {
        var isExpiredHold = dto.Status == SeatStatus.Held
            && dto.HeldUntilUtc is { } heldUntil
            && heldUntil <= utcNow;

        return isExpiredHold
            ? dto with { Status = SeatStatus.Available, HeldUntilUtc = null }
            : dto;
    }
}
