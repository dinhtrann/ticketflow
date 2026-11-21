using TicketFlow.Application.Dtos.Auth;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Application.Services;

public interface IJwtTokenProvider
{
    IssuedAccessToken GenerateAccessToken(User user);
}
