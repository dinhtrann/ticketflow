using TicketFlow.Domain.Enums;

namespace TicketFlow.Application.Dtos.Auth;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    DateTimeOffset CreatedAtUtc);
