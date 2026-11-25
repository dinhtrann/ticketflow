using Moq;
using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Events;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;
using TicketFlow.UnitTests.Helpers;

namespace TicketFlow.UnitTests.Application.Services;

public class EventServiceTests
{
    private readonly Mock<IEventRepository> _eventRepo = new();
    private readonly Mock<ISeatRepository> _seatRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly EventService _sut;

    public EventServiceTests()
    {
        _sut = new EventService(
            _eventRepo.Object,
            _seatRepo.Object,
            _uow.Object,
            new InMemoryQueryExecutor(),
            TestFixtures.CreateTimeProvider());
    }

    private void SetupTrackedEvent(Event? ev)
        => _eventRepo
            .Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);

    private void SetupEventLookup(Event? ev)
        => _eventRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);

    private void SetupStatusCounts(Guid eventId, SeatStatusCounts counts)
        => _seatRepo
            .Setup(r => r.GetStatusCountsAsync(eventId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(counts);

    // ---------- UpdateStatusAsync ----------

    [Fact]
    public async Task UpdateStatusAsync_DraftToPublished_NoSeats_Throws()
    {
        var ev = TestFixtures.CreateEvent(status: EventStatus.Draft);
        SetupTrackedEvent(ev);
        SetupStatusCounts(ev.Id, new SeatStatusCounts(0, 0, 0, 0));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.UpdateStatusAsync(ev.Id, new UpdateEventStatusRequest { Status = EventStatus.Published }));

        Assert.Equal("INVALID_STATUS_TRANSITION", ex.Code);
        Assert.Equal(EventStatus.Draft, ev.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_PublishAlreadyStarted_Throws()
    {
        var ev = TestFixtures.CreateEvent(
            status: EventStatus.Draft,
            startsAtUtc: TestFixtures.Now.AddHours(-1));
        SetupTrackedEvent(ev);
        SetupStatusCounts(ev.Id, new SeatStatusCounts(5, 5, 0, 0));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.UpdateStatusAsync(ev.Id, new UpdateEventStatusRequest { Status = EventStatus.Published }));

        Assert.Equal("INVALID_STATUS_TRANSITION", ex.Code);
    }

    [Fact]
    public async Task UpdateStatusAsync_DraftToPublished_WithSeats_Succeeds()
    {
        var ev = TestFixtures.CreateEvent(status: EventStatus.Draft);
        SetupTrackedEvent(ev);
        SetupStatusCounts(ev.Id, new SeatStatusCounts(10, 8, 0, 2));

        var result = await _sut.UpdateStatusAsync(
            ev.Id, new UpdateEventStatusRequest { Status = EventStatus.Published });

        Assert.Equal(EventStatus.Published, ev.Status);
        Assert.Equal(TestFixtures.Now, ev.UpdatedAtUtc);
        Assert.Equal(EventStatus.Published, result.Status);
        Assert.Equal(10, result.TotalSeats);
        Assert.Equal(8, result.AvailableSeats);
        Assert.Equal(0, result.HeldSeats);
        Assert.Equal(2, result.SoldSeats);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_PublishedToCancelled_ReleasesHeldSeats()
    {
        var ev = TestFixtures.CreateEvent(status: EventStatus.Published);
        SetupTrackedEvent(ev);
        var seat1 = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: Guid.CreateVersion7(),
            heldUntilUtc: TestFixtures.Now.AddMinutes(5),
            ev: ev);
        var seat2 = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: Guid.CreateVersion7(),
            heldUntilUtc: TestFixtures.Now.AddMinutes(5),
            ev: ev);
        _seatRepo
            .Setup(r => r.GetHeldSeatsTrackedAsync(ev.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Seat> { seat1, seat2 });
        SetupStatusCounts(ev.Id, new SeatStatusCounts(2, 2, 0, 0));

        var result = await _sut.UpdateStatusAsync(
            ev.Id, new UpdateEventStatusRequest { Status = EventStatus.Cancelled });

        Assert.Equal(EventStatus.Cancelled, ev.Status);
        Assert.Equal(TestFixtures.Now, ev.UpdatedAtUtc);
        foreach (var seat in new[] { seat1, seat2 })
        {
            Assert.Equal(SeatStatus.Available, seat.Status);
            Assert.Null(seat.HeldByUserId);
            Assert.Null(seat.HeldUntilUtc);
        }

        Assert.Equal(EventStatus.Cancelled, result.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    public static TheoryData<EventStatus, EventStatus> InvalidTransitions => new()
    {
        { EventStatus.Published, EventStatus.Draft },
        { EventStatus.Cancelled, EventStatus.Published },
        { EventStatus.Draft, EventStatus.Draft },
    };

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public async Task UpdateStatusAsync_InvalidTransition_Throws(EventStatus current, EventStatus target)
    {
        var ev = TestFixtures.CreateEvent(status: current);
        SetupTrackedEvent(ev);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.UpdateStatusAsync(ev.Id, new UpdateEventStatusRequest { Status = target }));

        Assert.Equal("INVALID_STATUS_TRANSITION", ex.Code);
        Assert.Equal(current, ev.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_EventNotFound_ThrowsNotFound()
    {
        SetupTrackedEvent(null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.UpdateStatusAsync(
                Guid.CreateVersion7(),
                new UpdateEventStatusRequest { Status = EventStatus.Published }));

        Assert.Equal("EVENT_NOT_FOUND", ex.Code);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task CreateAsync_PastStart_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.CreateAsync(new CreateEventRequest
            {
                Title = "Past Event",
                Venue = "Hall A",
                StartsAtUtc = TestFixtures.Now.AddHours(-1),
            }));

        Assert.Equal("VALIDATION_ERROR", ex.Code);
        _eventRepo.Verify(r => r.Add(It.IsAny<Event>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesDraft()
    {
        Event? added = null;
        _eventRepo.Setup(r => r.Add(It.IsAny<Event>())).Callback<Event>(e => added = e);

        var result = await _sut.CreateAsync(new CreateEventRequest
        {
            Title = "  My Event  ",
            Description = "  A nice show  ",
            Venue = "  Hall A  ",
            StartsAtUtc = TestFixtures.Now.AddDays(7),
        });

        Assert.NotNull(added);
        Assert.Equal(EventStatus.Draft, added!.Status);
        Assert.Equal("My Event", added.Title);
        Assert.Equal("A nice show", added.Description);
        Assert.Equal("Hall A", added.Venue);
        Assert.Equal(TestFixtures.Now.AddDays(7), added.StartsAtUtc);
        Assert.Equal(TestFixtures.Now, added.CreatedAtUtc);

        Assert.Equal(EventStatus.Draft, result.Status);
        Assert.Equal("My Event", result.Title);
        _eventRepo.Verify(r => r.Add(It.IsAny<Event>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- BulkCreateSeatsAsync ----------

    [Fact]
    public async Task BulkCreateSeats_DuplicateCodesInRequest_Throws()
    {
        var ev = TestFixtures.CreateEvent(status: EventStatus.Published);
        SetupEventLookup(ev);

        var request = new BulkSeatRequest
        {
            Seats = new List<BulkSeatItemRequest>
            {
                new() { SeatCode = "a1", Section = "A", Price = 100_000m },
                new() { SeatCode = "A1", Section = "A", Price = 100_000m },
            },
        };

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.BulkCreateSeatsAsync(ev.Id, request));

        Assert.Equal("DUPLICATE_SEAT_CODE", ex.Code);
        _seatRepo.Verify(
            r => r.AddRange(It.IsAny<IEnumerable<Seat>>()), Times.Never);
    }

    [Fact]
    public async Task BulkCreateSeats_CodeExistsInDb_Throws()
    {
        var ev = TestFixtures.CreateEvent(status: EventStatus.Published);
        SetupEventLookup(ev);
        _seatRepo
            .Setup(r => r.GetExistingSeatCodesAsync(
                ev.Id, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "B2" });

        var request = new BulkSeatRequest
        {
            Seats = new List<BulkSeatItemRequest>
            {
                new() { SeatCode = "b2", Section = "B", Price = 50_000m },
            },
        };

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.BulkCreateSeatsAsync(ev.Id, request));

        Assert.Equal("DUPLICATE_SEAT_CODE", ex.Code);
        _seatRepo.Verify(r => r.AddRange(It.IsAny<IEnumerable<Seat>>()), Times.Never);
    }

    [Fact]
    public async Task BulkCreateSeats_CancelledEvent_Throws()
    {
        var ev = TestFixtures.CreateEvent(status: EventStatus.Cancelled);
        SetupEventLookup(ev);

        var request = new BulkSeatRequest
        {
            Seats = new List<BulkSeatItemRequest>
            {
                new() { SeatCode = "C1", Section = "C", Price = 75_000m },
            },
        };

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.BulkCreateSeatsAsync(ev.Id, request));

        Assert.Equal("INVALID_STATUS_TRANSITION", ex.Code);
        _seatRepo.Verify(r => r.AddRange(It.IsAny<IEnumerable<Seat>>()), Times.Never);
    }

    [Fact]
    public async Task BulkCreateSeats_ValidRequest_AddsNormalizedSeats()
    {
        var ev = TestFixtures.CreateEvent(status: EventStatus.Published);
        SetupEventLookup(ev);
        _seatRepo
            .Setup(r => r.GetExistingSeatCodesAsync(
                ev.Id, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        IEnumerable<Seat>? added = null;
        _seatRepo
            .Setup(r => r.AddRange(It.IsAny<IEnumerable<Seat>>()))
            .Callback<IEnumerable<Seat>>(seats => added = seats);

        var result = await _sut.BulkCreateSeatsAsync(ev.Id, new BulkSeatRequest
        {
            Seats = new List<BulkSeatItemRequest>
            {
                new() { SeatCode = "a01", Section = " VIP ", Price = 100_000m },
                new() { SeatCode = " b02 ", Section = "Standard", Price = 200_000m },
            },
        });

        Assert.Equal(2, result.CreatedCount);
        Assert.NotNull(added);
        var seats = added!.ToList();
        Assert.Equal(2, seats.Count);
        Assert.Equal("A01", seats[0].SeatCode);
        Assert.Equal("VIP", seats[0].Section);
        Assert.Equal(SeatStatus.Available, seats[0].Status);
        Assert.Equal(ev.Id, seats[0].EventId);
        Assert.Equal("B02", seats[1].SeatCode);
        _seatRepo.Verify(r => r.AddRange(It.IsAny<IEnumerable<Seat>>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
