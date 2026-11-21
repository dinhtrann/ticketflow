using TicketFlow.Application.Dtos.Auth;

namespace TicketFlow.Application.Services;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);

    /// <summary>Idempotent — luôn thành công kể cả khi token không tồn tại hoặc không thuộc user.</summary>
    Task RevokeAsync(RevokeRequest request, Guid userId, CancellationToken ct = default);
}
