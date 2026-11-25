using Moq;
using TicketFlow.Application.Dtos.Auth;
using TicketFlow.Application.Interfaces;
using TicketFlow.Application.Options;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Entities;
using TicketFlow.Domain.Enums;
using TicketFlow.Domain.Exceptions;
using TicketFlow.UnitTests.Helpers;

namespace TicketFlow.UnitTests.Application.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtTokenProvider> _jwt = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(pw => "hashed-" + pw);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((pw, hash) => hash == "hashed-" + pw);
        _jwt.Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
            .Returns(new IssuedAccessToken("access-token", TestFixtures.Now.AddMinutes(15)));

        _sut = new AuthService(
            _userRepo.Object,
            _refreshTokenRepo.Object,
            _uow.Object,
            _hasher.Object,
            _jwt.Object,
            TestFixtures.CreateTimeProvider(),
            new AuthOptions { RefreshTokenLifetime = TimeSpan.FromDays(7) });
    }

    private void SetupUserLookup(User? user)
        => _userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

    private void SetupTokenLookup(RefreshToken? token)
        => _refreshTokenRepo
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

    // ---------- RegisterAsync ----------

    [Fact]
    public async Task RegisterAsync_NormalizesEmailAndAssignsCustomer()
    {
        SetupUserLookup(null);

        User? added = null;
        _userRepo.Setup(r => r.Add(It.IsAny<User>())).Callback<User>(u => added = u);

        var dto = await _sut.RegisterAsync(new RegisterRequest
        {
            Email = "  USER@Test.COM ",
            Password = "secret-123",
            FullName = "  Test User  ",
        });

        Assert.NotNull(added);
        Assert.Equal("user@test.com", added!.Email);
        Assert.Equal("hashed-secret-123", added.PasswordHash);
        Assert.Equal("Test User", added.FullName);
        Assert.Equal(UserRole.Customer, added.Role);
        Assert.Equal("user@test.com", dto.Email);
        Assert.Equal(UserRole.Customer, dto.Role);
        _hasher.Verify(h => h.Hash("secret-123"), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_EmailExists_ThrowsDomainValidation()
    {
        SetupUserLookup(TestFixtures.CreateUser(email: "user@test.com"));

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.RegisterAsync(new RegisterRequest
            {
                Email = "user@test.com",
                Password = "secret-123",
                FullName = "Test User",
            }));

        Assert.Equal("EMAIL_ALREADY_EXISTS", ex.Code);
        _userRepo.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
    }

    // ---------- LoginAsync ----------

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        SetupUserLookup(TestFixtures.CreateUser(passwordHash: "hashed-secret"));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest
            {
                Email = "user@test.com",
                Password = "wrong-password",
            }));

        Assert.Equal("INVALID_CREDENTIALS", ex.Code);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ThrowsUnauthorized()
    {
        SetupUserLookup(null);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.LoginAsync(new LoginRequest
            {
                Email = "nobody@test.com",
                Password = "whatever-123",
            }));

        Assert.Equal("INVALID_CREDENTIALS", ex.Code);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        var user = TestFixtures.CreateUser(passwordHash: "hashed-secret");
        SetupUserLookup(user);

        RefreshToken? addedToken = null;
        _refreshTokenRepo.Setup(r => r.Add(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => addedToken = t);

        var result = await _sut.LoginAsync(new LoginRequest
        {
            Email = "user@test.com",
            Password = "secret",
        });

        Assert.Equal("access-token", result.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.Equal(TestFixtures.Now.AddMinutes(15), result.AccessTokenExpiresAtUtc);
        Assert.Equal(TestFixtures.Now.AddDays(7), result.RefreshTokenExpiresAtUtc);
        Assert.Equal(user.Id, result.User.Id);

        Assert.NotNull(addedToken);
        Assert.Equal(user.Id, addedToken!.UserId);
        Assert.Equal(TestFixtures.Now.AddDays(7), addedToken.ExpiresAtUtc);
        Assert.Equal(TestFixtures.HashRefreshToken(result.RefreshToken), addedToken.TokenHash);
        _refreshTokenRepo.Verify(r => r.Add(It.IsAny<RefreshToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- RefreshAsync ----------

    [Fact]
    public async Task RefreshAsync_UnknownToken_ThrowsUnauthorized()
    {
        SetupTokenLookup(null);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.RefreshAsync(new RefreshRequest { RefreshToken = "unknown-token" }));

        Assert.Equal("INVALID_REFRESH_TOKEN", ex.Code);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ThrowsUnauthorized()
    {
        var user = TestFixtures.CreateUser();
        SetupTokenLookup(TestFixtures.CreateRefreshToken(user: user, expired: true));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.RefreshAsync(new RefreshRequest { RefreshToken = "expired-token" }));

        Assert.Equal("INVALID_REFRESH_TOKEN", ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_RevokeAllAndThrows()
    {
        var user = TestFixtures.CreateUser();
        var stored = TestFixtures.CreateRefreshToken(user: user, revoked: true);
        SetupTokenLookup(stored);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.RefreshAsync(new RefreshRequest { RefreshToken = "revoked-token" }));

        Assert.Equal("TOKEN_REUSE_DETECTED", ex.Code);
        _refreshTokenRepo.Verify(
            r => r.RevokeAllActiveForUserAsync(user.Id, TestFixtures.Now, It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesAndReturnsNew()
    {
        var user = TestFixtures.CreateUser();
        var stored = TestFixtures.CreateRefreshToken(user: user);
        SetupTokenLookup(stored);

        var addedTokens = new List<RefreshToken>();
        _refreshTokenRepo.Setup(r => r.Add(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => addedTokens.Add(t));

        var result = await _sut.RefreshAsync(new RefreshRequest { RefreshToken = "old-raw-token" });

        Assert.Equal(TestFixtures.Now, stored.RevokedAtUtc);
        Assert.False(string.IsNullOrEmpty(stored.ReplacedByTokenHash));

        var newToken = Assert.Single(addedTokens);
        Assert.Equal(user.Id, newToken.UserId);
        Assert.Equal(TestFixtures.Now.AddDays(7), newToken.ExpiresAtUtc);
        Assert.Equal(stored.ReplacedByTokenHash, newToken.TokenHash);
        Assert.Equal(TestFixtures.HashRefreshToken(result.RefreshToken), newToken.TokenHash);
        Assert.NotEqual("old-raw-token", result.RefreshToken);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal(user.Email, result.User.Email);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- RevokeAsync ----------

    [Fact]
    public async Task RevokeAsync_OwnActiveToken_SetsRevokedAt()
    {
        var userId = Guid.CreateVersion7();
        var stored = TestFixtures.CreateRefreshToken(userId: userId);
        SetupTokenLookup(stored);

        await _sut.RevokeAsync(new RevokeRequest { RefreshToken = "my-token" }, userId);

        Assert.Equal(TestFixtures.Now, stored.RevokedAtUtc);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_TokenOfOtherUser_DoesNothing()
    {
        var stored = TestFixtures.CreateRefreshToken(userId: Guid.CreateVersion7());
        SetupTokenLookup(stored);

        await _sut.RevokeAsync(new RevokeRequest { RefreshToken = "other-token" }, Guid.CreateVersion7());

        Assert.Null(stored.RevokedAtUtc);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
