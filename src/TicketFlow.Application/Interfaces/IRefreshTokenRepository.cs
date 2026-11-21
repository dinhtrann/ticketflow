using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Interfaces;

public interface IRefreshTokenRepository
{
    /// <summary>Tracking + Include(User) — dùng cho refresh rotation / reuse detection.</summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    void Add(RefreshToken token);
    Task RevokeAllActiveForUserAsync(Guid userId, DateTimeOffset revokedAtUtc, CancellationToken ct = default);
}
