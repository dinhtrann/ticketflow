using System.Security.Cryptography;
using System.Text;
using TicketFlow.Application.Dtos.Auth;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Options;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;

namespace TicketFlow.Application.Services;

public class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenProvider jwtTokenProvider,
    TimeProvider timeProvider,
    AuthOptions authOptions) : IAuthService
{
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        if (await userRepository.GetByEmailAsync(email, ct) is not null)
        {
            throw new DomainValidationException("EMAIL_ALREADY_EXISTS", "An account with this email already exists.");
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Role = UserRole.Customer,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        };

        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(ct);
        return ToUserDto(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEmailAsync(NormalizeEmail(request.Email), ct);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("INVALID_CREDENTIALS", "Invalid email or password.");
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var stored = await refreshTokenRepository.GetByTokenHashAsync(HashToken(request.RefreshToken), ct);

        if (stored is null)
        {
            throw new UnauthorizedException("INVALID_REFRESH_TOKEN", "Refresh token is invalid or expired.");
        }

        if (stored.RevokedAtUtc is not null)
        {
            await refreshTokenRepository.RevokeAllActiveForUserAsync(stored.UserId, now, ct);
            await unitOfWork.SaveChangesAsync(ct);
            throw new UnauthorizedException("TOKEN_REUSE_DETECTED", "Refresh token reuse detected. All sessions have been revoked.");
        }

        if (stored.ExpiresAtUtc <= now)
        {
            throw new UnauthorizedException("INVALID_REFRESH_TOKEN", "Refresh token is invalid or expired.");
        }

        var (rawToken, newToken) = CreateRefreshToken(stored.UserId, now);
        stored.RevokedAtUtc = now;
        stored.ReplacedByTokenHash = newToken.TokenHash;
        refreshTokenRepository.Add(newToken);
        await unitOfWork.SaveChangesAsync(ct);

        var accessToken = jwtTokenProvider.GenerateAccessToken(stored.User);
        return new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            rawToken,
            newToken.ExpiresAtUtc,
            new AuthUserDto(stored.User.Id, stored.User.Email, stored.User.FullName, stored.User.Role));
    }

    public async Task RevokeAsync(RevokeRequest request, Guid userId, CancellationToken ct = default)
    {
        var stored = await refreshTokenRepository.GetByTokenHashAsync(HashToken(request.RefreshToken), ct);
        if (stored is null || stored.UserId != userId || stored.RevokedAtUtc is not null)
        {
            return;
        }

        stored.RevokedAtUtc = timeProvider.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = jwtTokenProvider.GenerateAccessToken(user);
        var (rawToken, refreshToken) = CreateRefreshToken(user.Id, timeProvider.GetUtcNow());
        refreshTokenRepository.Add(refreshToken);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            rawToken,
            refreshToken.ExpiresAtUtc,
            new AuthUserDto(user.Id, user.Email, user.FullName, user.Role));
    }

    private (string RawToken, RefreshToken Entity) CreateRefreshToken(Guid userId, DateTimeOffset now)
    {
        var rawToken = GenerateRefreshToken();
        var entity = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = now + authOptions.RefreshTokenLifetime,
            CreatedAtUtc = now,
        };
        return (rawToken, entity);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static UserDto ToUserDto(User user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.CreatedAtUtc);
}
