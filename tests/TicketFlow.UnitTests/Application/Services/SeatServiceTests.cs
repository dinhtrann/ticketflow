using Moq;
using TicketFlow.Application.Dtos.Seats;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Options;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;
using TicketFlow.UnitTests.Helpers;

namespace TicketFlow.UnitTests.Application.Services;

public class SeatServiceTests
{
    private readonly Mock<ISeatRepository> _seatRepo = new();
    private readonly Mock<IEventRepository> _eventRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly SeatService _sut;
    private readonly Guid _userId = Guid.CreateVersion7();

    public SeatServiceTests()
    {
        _sut = new SeatService(
            _seatRepo.Object,
            _eventRepo.Object,
            _uow.Object,
            new InMemoryQueryExecutor(),
            TestFixtures.CreateTimeProvider(),
            new BookingOptions { HoldDurationMinutes = 10 });
    }

    private void SetupSeat(Seat? seat)
        => _seatRepo
            .Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

    // ---------- HoldAsync ----------

    [Fact]
    public async Task HoldAsync_AvailableSeat_SetsHeldAndReturnsResponse()
    {
        var ev = TestFixtures.CreateEvent();
        var seat = TestFixtures.CreateSeat(ev: ev);
        SetupSeat(seat);

        var result = await _sut.HoldAsync(seat.Id, _userId);

        Assert.Equal(SeatStatus.Held, seat.Status);
        Assert.Equal(_userId, seat.HeldByUserId);
        Assert.Equal(TestFixtures.Now.AddMinutes(10), seat.HeldUntilUtc);
        Assert.Equal(SeatStatus.Held, result.Status);
        Assert.Equal(_userId, result.HeldByUserId);
        Assert.Equal(seat.HeldUntilUtc, result.HeldUntilUtc);
        Assert.False(string.IsNullOrEmpty(result.RowVersion));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HoldAsync_SeatNotFound_ThrowsNotFound()
    {
        SetupSeat(null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.HoldAsync(Guid.CreateVersion7(), _userId));

        Assert.Equal("SEAT_NOT_FOUND", ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HoldAsync_SoldSeat_ThrowsDomainValidation()
    {
        var seat = TestFixtures.CreateSeat(status: SeatStatus.Sold, ev: TestFixtures.CreateEvent());
        SetupSeat(seat);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.HoldAsync(seat.Id, _userId));

        Assert.Equal("SEAT_ALREADY_SOLD", ex.Code);
    }

    [Fact]
    public async Task HoldAsync_HeldByOtherNotExpired_ThrowsDomainValidation()
    {
        var seat = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: Guid.CreateVersion7(),
            heldUntilUtc: TestFixtures.Now.AddMinutes(5),
            ev: TestFixtures.CreateEvent());
        SetupSeat(seat);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.HoldAsync(seat.Id, _userId));

        Assert.Equal("SEAT_ALREADY_HELD", ex.Code);
    }

    [Fact]
    public async Task HoldAsync_HeldExpired_CanReHold()
    {
        var otherUserId = Guid.CreateVersion7();
        var seat = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: otherUserId,
            heldUntilUtc: TestFixtures.Now.AddMinutes(-1),
            ev: TestFixtures.CreateEvent());
        SetupSeat(seat);

        await _sut.HoldAsync(seat.Id, _userId);

        Assert.Equal(SeatStatus.Held, seat.Status);
        Assert.Equal(_userId, seat.HeldByUserId);
        Assert.Equal(TestFixtures.Now.AddMinutes(10), seat.HeldUntilUtc);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HoldAsync_EventDraft_ThrowsNotBookable()
    {
        var seat = TestFixtures.CreateSeat(ev: TestFixtures.CreateEvent(status: EventStatus.Draft));
        SetupSeat(seat);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.HoldAsync(seat.Id, _userId));

        Assert.Equal("EVENT_NOT_BOOKABLE", ex.Code);
    }

    [Fact]
    public async Task HoldAsync_EventStarted_ThrowsNotBookable()
    {
        var seat = TestFixtures.CreateSeat(
            ev: TestFixtures.CreateEvent(startsAtUtc: TestFixtures.Now.AddHours(-1)));
        SetupSeat(seat);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.HoldAsync(seat.Id, _userId));

        Assert.Equal("EVENT_NOT_BOOKABLE", ex.Code);
    }

    // ---------- ReleaseAsync ----------

    [Fact]
    public async Task ReleaseAsync_OwnerHeldSeat_SetsAvailable()
    {
        var seat = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: _userId,
            heldUntilUtc: TestFixtures.Now.AddMinutes(5),
            ev: TestFixtures.CreateEvent());
        SetupSeat(seat);

        var result = await _sut.ReleaseAsync(seat.Id, _userId, isAdmin: false);

        Assert.Equal(SeatStatus.Available, seat.Status);
        Assert.Null(seat.HeldByUserId);
        Assert.Null(seat.HeldUntilUtc);
        Assert.Equal(SeatStatus.Available, result.Status);
        Assert.Equal(TestFixtures.Now, result.ReleasedAtUtc);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_NotOwnerNonAdmin_ThrowsForbidden()
    {
        var seat = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: Guid.CreateVersion7(),
            heldUntilUtc: TestFixtures.Now.AddMinutes(5),
            ev: TestFixtures.CreateEvent());
        SetupSeat(seat);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.ReleaseAsync(seat.Id, _userId, isAdmin: false));

        Assert.Equal(SeatStatus.Held, seat.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseAsync_AdminCanReleaseOthersHold()
    {
        var seat = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: Guid.CreateVersion7(),
            heldUntilUtc: TestFixtures.Now.AddMinutes(5),
            ev: TestFixtures.CreateEvent());
        SetupSeat(seat);

        var result = await _sut.ReleaseAsync(seat.Id, _userId, isAdmin: true);

        Assert.Equal(SeatStatus.Available, result.Status);
        Assert.Equal(SeatStatus.Available, seat.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_AvailableSeat_ThrowsDomainValidation()
    {
        var seat = TestFixtures.CreateSeat(ev: TestFixtures.CreateEvent());
        SetupSeat(seat);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ReleaseAsync(seat.Id, _userId, isAdmin: false));

        Assert.Equal("SEAT_NOT_HELD", ex.Code);
    }

    [Fact]
    public async Task HoldAsync_SaveChangesConcurrencyConflict_PropagatesConflict()
    {
        var seat = TestFixtures.CreateSeat(ev: TestFixtures.CreateEvent());
        SetupSeat(seat);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => _sut.HoldAsync(seat.Id, _userId));

        Assert.Equal("SEAT_CONCURRENCY_CONFLICT", ex.Code);
    }

    // ---------- GetSeatsAsync ----------

    [Fact]
    public async Task GetSeatsAsync_ExpiredHold_MappedAsAvailable()
    {
        var ev = TestFixtures.CreateEvent();
        var seat = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: Guid.CreateVersion7(),
            heldUntilUtc: TestFixtures.Now.AddMinutes(-1),
            ev: ev);
        _seatRepo.Setup(r => r.GetQueryable()).Returns(new List<Seat> { seat }.AsQueryable());
        _eventRepo.Setup(r => r.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ev);

        var result = await _sut.GetSeatsAsync(ev.Id, new SeatSearchQuery(), isAdmin: false);

        var dto = Assert.Single(result.Items);
        Assert.Equal(SeatStatus.Available, dto.Status);
        Assert.Null(dto.HeldUntilUtc);
    }

    [Fact]
    public async Task GetSeatsAsync_DraftEventNonAdmin_ThrowsNotFound()
    {
        var ev = TestFixtures.CreateEvent(status: EventStatus.Draft);
        _eventRepo.Setup(r => r.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ev);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.GetSeatsAsync(ev.Id, new SeatSearchQuery(), isAdmin: false));

        Assert.Equal("EVENT_NOT_FOUND", ex.Code);
    }
}
