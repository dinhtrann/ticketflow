using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TicketFlow.Application.Dtos.Auth;
using TicketFlow.Application.Options;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Security;

public class JwtTokenProvider(JwtOptions options, TimeProvider timeProvider) : IJwtTokenProvider
{
    private const int MinSecretKeyBytes = 32;

    public IssuedAccessToken GenerateAccessToken(User user)
    {
        var keyBytes = Encoding.UTF8.GetBytes(options.SecretKey);
        if (keyBytes.Length < MinSecretKeyBytes)
        {
            throw new InvalidOperationException($"Jwt:SecretKey must be at least {MinSecretKeyBytes} bytes.");
        }

        var now = timeProvider.GetUtcNow();
        var expiresAtUtc = now + options.AccessTokenLifetime;

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("email", user.Email),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
