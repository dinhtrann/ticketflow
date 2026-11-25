using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;

namespace TicketFlow.UnitTests.Helpers;

/// <summary>
/// Factory tạo entity + hằng số thời gian + hash helpers cho unit test.
/// Mọi thời gian đều neo vào <see cref="Now"/> — KHÔNG dùng DateTime.Now/UtcNow.
/// </summary>
public static class TestFixtures
{
    /// <summary>Thời điểm gốc cố định cho toàn bộ test suite.</summary>
    public static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static FakeTimeProvider CreateTimeProvider() => new(Now);

    public static User CreateUser(
        Guid? id = null,
        string email = "user@test.com",
        string passwordHash = "hashed-password",
        string fullName = "Test User",
        UserRole role = UserRole.Customer)
        => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            Email = email,
            PasswordHash = passwordHash,
            FullName = fullName,
            Role = role,
            CreatedAtUtc = Now,
        };

    /// <summary>
    /// Event mặc định Published, bắt đầu sau 7 ngày (bookable).
    /// </summary>
    public static Event CreateEvent(
        EventStatus status = EventStatus.Published,
        DateTimeOffset? startsAtUtc = null,
        string title = "Test Event",
        string venue = "Test Venue")
        => new()
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            Description = "Test description",
            Venue = venue,
            StartsAtUtc = startsAtUtc ?? Now.AddDays(7),
            Status = status,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
        };

    /// <summary>
    /// Seat gắn sẵn navigation <see cref="Seat.Event"/> — SeatService/OrderService đọc seat.Event
    /// nên BẮT BUỘC truyền event (hoặc truyền <paramref name="ev"/> đã tạo trước).
    /// </summary>
    public static Seat CreateSeat(
        Guid? eventId = null,
        SeatStatus status = SeatStatus.Available,
        decimal price = 100_000m,
        string seatCode = "A1",
        string section = "A",
        Guid? heldByUserId = null,
        DateTimeOffset? heldUntilUtc = null,
        Event? ev = null)
    {
        var resolvedEvent = ev ?? CreateEvent();
        return new Seat
        {
            Id = Guid.CreateVersion7(),
            EventId = eventId ?? resolvedEvent.Id,
            SeatCode = seatCode,
            Section = section,
            Price = price,
            Status = status,
            HeldByUserId = heldByUserId,
            HeldUntilUtc = heldUntilUtc,
            RowVersion = [1, 2, 3, 4],
            Event = resolvedEvent,
        };
    }

    /// <summary>
    /// Order với <see cref="Order.Event"/> và <see cref="Order.Seat"/> navigation đã gán
    /// (OrderDto.FromOrder đọc order.Event.Title, order.Seat.SeatCode/Section).
    /// </summary>
    public static Order CreateOrder(
        Guid? userId = null,
        Guid? eventId = null,
        Guid? seatId = null,
        string idempotencyKey = "test-idem-key-0001",
        decimal totalAmount = 100_000m,
        OrderStatus status = OrderStatus.Confirmed,
        Event? ev = null,
        Seat? seat = null,
        User? user = null)
    {
        var resolvedEvent = ev ?? CreateEvent();
        var resolvedSeat = seat ?? CreateSeat(eventId: resolvedEvent.Id, ev: resolvedEvent);
        var resolvedUserId = userId ?? Guid.CreateVersion7();
        return new Order
        {
            Id = Guid.CreateVersion7(),
            UserId = resolvedUserId,
            EventId = eventId ?? resolvedEvent.Id,
            SeatId = seatId ?? resolvedSeat.Id,
            TotalAmount = totalAmount,
            Status = status,
            IdempotencyKey = idempotencyKey,
            RequestHash = ComputeRequestHash(seatId ?? resolvedSeat.Id),
            CreatedAtUtc = Now,
            Event = resolvedEvent,
            Seat = resolvedSeat,
            User = user ?? CreateUser(id: resolvedUserId),
        };
    }

    /// <summary>
    /// RefreshToken gắn sẵn navigation <see cref="RefreshToken.User"/>
    /// (AuthService.RefreshAsync đọc stored.User).
    /// </summary>
    public static RefreshToken CreateRefreshToken(
        Guid? userId = null,
        User? user = null,
        bool revoked = false,
        bool expired = false,
        string rawTokenHash = "stored-token-hash")
    {
        var resolvedUser = user ?? CreateUser(id: userId);
        var uid = userId ?? resolvedUser.Id;
        return new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = uid,
            TokenHash = rawTokenHash,
            ExpiresAtUtc = expired ? Now.AddDays(-1) : Now.AddDays(7),
            CreatedAtUtc = Now.AddDays(-1),
            RevokedAtUtc = revoked ? Now.AddHours(-1) : null,
            User = resolvedUser,
        };
    }

    /// <summary>
    /// Replicate AuthService.HashToken (private): SHA256 UTF8 → hex lower 64 ký tự.
    /// Dùng để tính TokenHash khi cần match đúng raw token trong mock.
    /// </summary>
    public static string HashRefreshToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)))
            .ToLowerInvariant();

    /// <summary>
    /// Replicate OrderService.ComputeRequestHash (private): SHA256 ASCII của seatId "D" → hex lower.
    /// Dùng để tạo Order.RequestHash khớp payload confirm.
    /// </summary>
    public static string ComputeRequestHash(Guid seatId)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes($"{seatId:D}")))
            .ToLowerInvariant();
}
