using Moq;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;
using TicketFlow.UnitTests.Helpers;

namespace TicketFlow.UnitTests.Application.Services;

public class OrderServiceTests
{
    private const string ValidKey = "key-12345678";

    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly Mock<ISeatRepository> _seatRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly OrderService _sut;
    private readonly Guid _userId = Guid.CreateVersion7();

    public OrderServiceTests()
    {
        _sut = new OrderService(
            _orderRepo.Object,
            _seatRepo.Object,
            _uow.Object,
            new InMemoryQueryExecutor(),
            TestFixtures.CreateTimeProvider());
    }

    private void SetupSeat(Seat? seat)
        => _seatRepo
            .Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seat);

    private void SetupNoExistingOrder()
        => _orderRepo
            .Setup(r => r.FindByUserAndIdempotencyKeyAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

    /// <summary>Seat Held hợp lệ bởi _userId, event Published tương lai — fixture chuẩn cho confirm.</summary>
    private Seat CreateHeldSeatByUser(EventStatus eventStatus = EventStatus.Published)
        => TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: _userId,
            heldUntilUtc: TestFixtures.Now.AddMinutes(10),
            ev: TestFixtures.CreateEvent(status: eventStatus));

    // ---------- ConfirmAsync ----------

    [Fact]
    public async Task ConfirmAsync_ValidHold_CreatesConfirmedOrder()
    {
        var seat = CreateHeldSeatByUser();
        SetupSeat(seat);
        SetupNoExistingOrder();

        Order? added = null;
        _orderRepo.Setup(r => r.Add(It.IsAny<Order>()))
            .Callback<Order>(o => added = o);

        var result = await _sut.ConfirmAsync(seat.Id, ValidKey, _userId);

        Assert.True(result.IsNew);
        Assert.NotNull(added);
        Assert.Equal(OrderStatus.Confirmed, added!.Status);
        Assert.Equal(seat.Price, added.TotalAmount);
        Assert.Equal(seat.Id, added.SeatId);
        Assert.Equal(_userId, added.UserId);
        Assert.Equal(ValidKey, added.IdempotencyKey);
        Assert.Equal(TestFixtures.ComputeRequestHash(seat.Id), added.RequestHash);
        Assert.Equal(added!.Id, result.Order.OrderId);
        Assert.Equal(OrderStatus.Confirmed, result.Order.Status);

        Assert.Equal(SeatStatus.Sold, seat.Status);
        Assert.Null(seat.HeldUntilUtc);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_NullIdempotencyKey_ThrowsValidation()
    {
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ConfirmAsync(Guid.CreateVersion7(), null, _userId));

        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    public static TheoryData<string> InvalidIdempotencyKeys => new()
    {
        "abc",
        "has space here",
        new string('a', 129),
    };

    [Theory]
    [MemberData(nameof(InvalidIdempotencyKeys))]
    public async Task ConfirmAsync_InvalidIdempotencyKey_ThrowsValidation(string key)
    {
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ConfirmAsync(Guid.CreateVersion7(), key, _userId));

        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Fact]
    public async Task ConfirmAsync_ExistingKeySameHash_ReturnsExisting()
    {
        var seat = CreateHeldSeatByUser();
        var existing = TestFixtures.CreateOrder(
            userId: _userId,
            seatId: seat.Id,
            idempotencyKey: ValidKey,
            seat: seat,
            ev: seat.Event);
        _orderRepo
            .Setup(r => r.FindByUserAndIdempotencyKeyAsync(_userId, ValidKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _sut.ConfirmAsync(seat.Id, ValidKey, _userId);

        Assert.False(result.IsNew);
        Assert.Equal(existing.Id, result.Order.OrderId);
        Assert.Equal(SeatStatus.Held, seat.Status);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _orderRepo.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_ExistingKeyDifferentHash_ThrowsMismatch()
    {
        var seat = CreateHeldSeatByUser();
        var existing = TestFixtures.CreateOrder(
            userId: _userId,
            seatId: Guid.CreateVersion7(),
            idempotencyKey: ValidKey,
            seat: seat,
            ev: seat.Event);
        _orderRepo
            .Setup(r => r.FindByUserAndIdempotencyKeyAsync(_userId, ValidKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ConfirmAsync(seat.Id, ValidKey, _userId));

        Assert.Equal("IDEMPOTENCY_KEY_PAYLOAD_MISMATCH", ex.Code);
    }

    [Fact]
    public async Task ConfirmAsync_HoldExpired_ThrowsDomainValidation()
    {
        var seat = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: _userId,
            heldUntilUtc: TestFixtures.Now.AddSeconds(-1),
            ev: TestFixtures.CreateEvent());
        SetupSeat(seat);
        SetupNoExistingOrder();

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ConfirmAsync(seat.Id, ValidKey, _userId));

        Assert.Equal("SEAT_HOLD_EXPIRED", ex.Code);
    }

    [Fact]
    public async Task ConfirmAsync_HeldByOtherUser_ThrowsDomainValidation()
    {
        var seat = TestFixtures.CreateSeat(
            status: SeatStatus.Held,
            heldByUserId: Guid.CreateVersion7(),
            heldUntilUtc: TestFixtures.Now.AddMinutes(10),
            ev: TestFixtures.CreateEvent());
        SetupSeat(seat);
        SetupNoExistingOrder();

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ConfirmAsync(seat.Id, ValidKey, _userId));

        Assert.Equal("SEAT_NOT_HELD_BY_USER", ex.Code);
    }

    [Fact]
    public async Task ConfirmAsync_SoldSeat_ThrowsDomainValidation()
    {
        var seat = TestFixtures.CreateSeat(status: SeatStatus.Sold, ev: TestFixtures.CreateEvent());
        SetupSeat(seat);
        SetupNoExistingOrder();

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ConfirmAsync(seat.Id, ValidKey, _userId));

        Assert.Equal("SEAT_ALREADY_SOLD", ex.Code);
    }

    [Fact]
    public async Task ConfirmAsync_AvailableSeat_ThrowsNotHeldByUser()
    {
        var seat = TestFixtures.CreateSeat(ev: TestFixtures.CreateEvent());
        SetupSeat(seat);
        SetupNoExistingOrder();

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ConfirmAsync(seat.Id, ValidKey, _userId));

        Assert.Equal("SEAT_NOT_HELD_BY_USER", ex.Code);
    }

    [Fact]
    public async Task ConfirmAsync_UoWThrowsUniqueViolation_FallsBackToExisting()
    {
        var seat = CreateHeldSeatByUser();
        SetupSeat(seat);
        SetupNoExistingOrder();

        var existing = TestFixtures.CreateOrder(
            userId: _userId,
            seatId: seat.Id,
            idempotencyKey: ValidKey,
            seat: seat,
            ev: seat.Event);
        _orderRepo
            .SetupSequence(r => r.FindByUserAndIdempotencyKeyAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null)
            .ReturnsAsync(existing);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueConstraintViolationException("UX_Order_Idempotency", "duplicate"));

        var result = await _sut.ConfirmAsync(seat.Id, ValidKey, _userId);

        Assert.False(result.IsNew);
        Assert.Equal(existing.Id, result.Order.OrderId);
        _orderRepo.Verify(
            r => r.FindByUserAndIdempotencyKeyAsync(It.IsAny<Guid>(), ValidKey, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ConfirmAsync_UoWThrowsConcurrency_NoExistingOrder_ThrowsConflict()
    {
        var seat = CreateHeldSeatByUser();
        SetupSeat(seat);
        SetupNoExistingOrder();
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => _sut.ConfirmAsync(seat.Id, ValidKey, _userId));

        _orderRepo.Verify(
            r => r.FindByUserAndIdempotencyKeyAsync(It.IsAny<Guid>(), ValidKey, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ConfirmAsync_SeatNotFound_ThrowsNotFound()
    {
        SetupSeat(null);
        SetupNoExistingOrder();

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.ConfirmAsync(Guid.CreateVersion7(), ValidKey, _userId));

        Assert.Equal("SEAT_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task ConfirmAsync_EventNotBookable_ThrowsDomainValidation()
    {
        var seat = CreateHeldSeatByUser(EventStatus.Cancelled);
        SetupSeat(seat);
        SetupNoExistingOrder();

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ConfirmAsync(seat.Id, ValidKey, _userId));

        Assert.Equal("EVENT_NOT_BOOKABLE", ex.Code);
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsync_OtherUserNonAdmin_ThrowsForbidden()
    {
        var order = TestFixtures.CreateOrder(userId: Guid.CreateVersion7());
        _orderRepo
            .Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _sut.GetByIdAsync(order.Id, _userId, isAdmin: false));
    }

    [Fact]
    public async Task GetByIdAsync_AdminCanReadAnyOrder()
    {
        var order = TestFixtures.CreateOrder(userId: Guid.CreateVersion7());
        _orderRepo
            .Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var dto = await _sut.GetByIdAsync(order.Id, _userId, isAdmin: true);

        Assert.Equal(order.Id, dto.OrderId);
        Assert.Equal(order.Event.Title, dto.EventTitle);
        Assert.Equal(order.Seat.SeatCode, dto.SeatCode);
    }

    [Fact]
    public async Task GetByIdAsync_OrderNotFound_ThrowsNotFound()
    {
        _orderRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.GetByIdAsync(Guid.CreateVersion7(), _userId, isAdmin: false));

        Assert.Equal("ORDER_NOT_FOUND", ex.Code);
    }
}
