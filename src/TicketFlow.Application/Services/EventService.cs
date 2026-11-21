using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Events;
using TicketFlow.Application.Interfaces;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;

namespace TicketFlow.Application.Services;

public class EventService(
    IEventRepository eventRepository,
    ISeatRepository seatRepository,
    IUnitOfWork unitOfWork,
    IAsyncQueryExecutor queryExecutor,
    TimeProvider timeProvider) : IEventService
{
    private static readonly SeatStatusCounts EmptyCounts = new(0, 0, 0, 0);

    public async Task<PagedResult<EventSummaryDto>> SearchAsync(
        EventSearchQuery query,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var utcNow = timeProvider.GetUtcNow();

        var events = eventRepository.GetQueryable();
        events = isAdmin
            ? ApplyAdminStatusFilter(events, query.Status)
            : events.Where(e => e.Status == EventStatus.Published);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            events = events.Where(e => e.Title.Contains(term) || e.Venue.Contains(term));
        }

        if (query.FromUtc is { } fromUtc)
        {
            events = events.Where(e => e.StartsAtUtc >= fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            events = events.Where(e => e.StartsAtUtc <= toUtc);
        }

        events = ApplySort(events, query.SortBy);

        var totalCount = await queryExecutor.CountAsync(events, ct);
        var projected = events
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventSummaryDto(
                e.Id,
                e.Title,
                e.Venue,
                e.StartsAtUtc,
                e.Status,
                0,
                0));

        var pageItems = await queryExecutor.ToListAsync(projected, ct);
        if (pageItems.Count == 0)
        {
            return new PagedResult<EventSummaryDto>(pageItems, page, pageSize, totalCount);
        }

        var eventIds = pageItems.Select(i => i.Id).ToArray();
        var counts = await seatRepository.GetStatusCountsAsync(eventIds, utcNow, ct);
        var items = pageItems
            .Select(dto => counts.TryGetValue(dto.Id, out var count) ? WithSummaryCounts(dto, count) : dto)
            .ToList();

        return new PagedResult<EventSummaryDto>(items, page, pageSize, totalCount);
    }

    public async Task<EventDetailDto> GetByIdAsync(Guid eventId, bool isAdmin, CancellationToken ct = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        var query = eventRepository.GetQueryable()
            .Where(e => e.Id == eventId)
            .Select(e => new EventDetailDto(
                e.Id,
                e.Title,
                e.Description,
                e.Venue,
                e.StartsAtUtc,
                e.Status,
                0,
                0,
                0,
                0,
                e.CreatedAtUtc,
                e.UpdatedAtUtc));

        var dto = await queryExecutor.FirstOrDefaultAsync(query, ct);
        if (dto is null || (!isAdmin && dto.Status != EventStatus.Published))
        {
            throw new NotFoundException("EVENT_NOT_FOUND", "Event not found.");
        }

        var counts = await seatRepository.GetStatusCountsAsync(eventId, utcNow, ct);
        return WithDetailCounts(dto, counts);
    }

    public async Task<EventDetailDto> CreateAsync(CreateEventRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        if (request.StartsAtUtc <= now)
        {
            throw new DomainValidationException("VALIDATION_ERROR", "startsAtUtc must be in the future.");
        }

        var ev = new Event
        {
            Id = Guid.CreateVersion7(),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Venue = request.Venue.Trim(),
            StartsAtUtc = request.StartsAtUtc,
            Status = EventStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        eventRepository.Add(ev);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDetail(ev, EmptyCounts);
    }

    public async Task<EventDetailDto> UpdateStatusAsync(
        Guid eventId,
        UpdateEventStatusRequest request,
        CancellationToken ct = default)
    {
        var ev = await eventRepository.GetByIdTrackedAsync(eventId, ct)
            ?? throw new NotFoundException("EVENT_NOT_FOUND", "Event not found.");

        var now = timeProvider.GetUtcNow();
        EnsureValidTransition(ev.Status, request.Status);

        if (request.Status == EventStatus.Published)
        {
            if (ev.StartsAtUtc <= now)
            {
                throw new DomainValidationException("INVALID_STATUS_TRANSITION", "Cannot publish an event that has already started.");
            }

            var publishCounts = await seatRepository.GetStatusCountsAsync(eventId, now, ct);
            if (publishCounts.TotalSeats == 0)
            {
                throw new DomainValidationException("INVALID_STATUS_TRANSITION", "Cannot publish an event without seats.");
            }
        }

        if (ev.Status == EventStatus.Published && request.Status == EventStatus.Cancelled)
        {
            var heldSeats = await seatRepository.GetHeldSeatsTrackedAsync(eventId, ct);
            foreach (var seat in heldSeats)
            {
                seat.Status = SeatStatus.Available;
                seat.HeldByUserId = null;
                seat.HeldUntilUtc = null;
            }
        }

        ev.Status = request.Status;
        ev.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(ct);

        var updatedCounts = await seatRepository.GetStatusCountsAsync(eventId, timeProvider.GetUtcNow(), ct);
        return ToDetail(ev, updatedCounts);
    }

    public async Task<BulkSeatResponse> BulkCreateSeatsAsync(
        Guid eventId,
        BulkSeatRequest request,
        CancellationToken ct = default)
    {
        var ev = await eventRepository.GetByIdAsync(eventId, ct)
            ?? throw new NotFoundException("EVENT_NOT_FOUND", "Event not found.");

        if (ev.Status == EventStatus.Cancelled)
        {
            throw new DomainValidationException("INVALID_STATUS_TRANSITION", "Cannot add seats to a cancelled event.");
        }

        var normalized = request.Seats
            .Select(s => new
            {
                SeatCode = s.SeatCode.Trim().ToUpperInvariant(),
                Section = s.Section.Trim(),
                s.Price,
            })
            .ToList();

        if (normalized.Count is < 1 or > 500)
        {
            throw new DomainValidationException("VALIDATION_ERROR", "Seats must contain between 1 and 500 items.");
        }

        var duplicateCodes = normalized
            .GroupBy(s => s.SeatCode)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateCodes.Count > 0)
        {
            throw new DomainValidationException("DUPLICATE_SEAT_CODE", $"Duplicate seat codes in request: {string.Join(", ", duplicateCodes)}.");
        }

        var seatCodes = normalized.Select(s => s.SeatCode).ToArray();
        var existingCodes = await seatRepository.GetExistingSeatCodesAsync(eventId, seatCodes, ct);
        if (existingCodes.Count > 0)
        {
            throw new DomainValidationException("DUPLICATE_SEAT_CODE", $"Seat code '{existingCodes.First()}' already exists for this event.");
        }

        var seats = normalized
            .Select(s => new Seat
            {
                Id = Guid.CreateVersion7(),
                EventId = eventId,
                SeatCode = s.SeatCode,
                Section = s.Section,
                Price = s.Price,
                Status = SeatStatus.Available,
            })
            .ToList();

        seatRepository.AddRange(seats);
        await unitOfWork.SaveChangesAsync(ct);

        var utcNow = timeProvider.GetUtcNow();
        return new BulkSeatResponse(
            eventId,
            seats.Count,
            seats.Select(s => SeatDto.FromSeat(s, utcNow)).ToList());
    }

    private static IQueryable<Event> ApplyAdminStatusFilter(IQueryable<Event> events, EventStatus? status)
    {
        if (status is null)
        {
            return events;
        }

        var value = status.Value;
        return events.Where(e => e.Status == value);
    }

    private static IQueryable<Event> ApplySort(IQueryable<Event> events, string? sortBy)
    {
        var normalized = sortBy?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" or "startsatasc" => events.OrderBy(e => e.StartsAtUtc).ThenBy(e => e.Id),
            "startsatdesc" => events.OrderByDescending(e => e.StartsAtUtc).ThenBy(e => e.Id),
            "createdatdesc" => events.OrderByDescending(e => e.CreatedAtUtc).ThenBy(e => e.Id),
            _ => throw new DomainValidationException("VALIDATION_ERROR", "sortBy must be one of: startsAtAsc, startsAtDesc, createdAtDesc."),
        };
    }

    private static void EnsureValidTransition(EventStatus current, EventStatus target)
    {
        var isValid = (current, target) switch
        {
            (EventStatus.Draft, EventStatus.Published) => true,
            (EventStatus.Draft, EventStatus.Cancelled) => true,
            (EventStatus.Published, EventStatus.Cancelled) => true,
            _ => false,
        };

        if (!isValid)
        {
            throw new DomainValidationException("INVALID_STATUS_TRANSITION", $"Cannot transition event from {current} to {target}.");
        }
    }

    private static EventSummaryDto WithSummaryCounts(EventSummaryDto dto, SeatStatusCounts counts) =>
        dto with
        {
            TotalSeats = counts.TotalSeats,
            AvailableSeats = counts.AvailableSeats,
        };

    private static EventDetailDto WithDetailCounts(EventDetailDto dto, SeatStatusCounts counts) =>
        dto with
        {
            TotalSeats = counts.TotalSeats,
            AvailableSeats = counts.AvailableSeats,
            HeldSeats = counts.HeldSeats,
            SoldSeats = counts.SoldSeats,
        };

    private static EventDetailDto ToDetail(Event ev, SeatStatusCounts counts) =>
        new(
            ev.Id,
            ev.Title,
            ev.Description,
            ev.Venue,
            ev.StartsAtUtc,
            ev.Status,
            counts.TotalSeats,
            counts.AvailableSeats,
            counts.HeldSeats,
            counts.SoldSeats,
            ev.CreatedAtUtc,
            ev.UpdatedAtUtc);
}
