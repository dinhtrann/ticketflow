using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketFlow.Application.Dtos.Auth;
using TicketFlow.Application.Services;
using TicketFlow.Domain.Exceptions;

namespace TicketFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await authService.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await authService.LoginAsync(request, ct);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var response = await authService.RefreshAsync(request, ct);
        return Ok(response);
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke(RevokeRequest request, CancellationToken ct)
    {
        var subject = User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
        {
            throw new UnauthorizedException();
        }

        await authService.RevokeAsync(request, userId, ct);
        return NoContent();
    }
}
