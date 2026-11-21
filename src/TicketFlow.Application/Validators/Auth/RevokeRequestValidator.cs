using FluentValidation;
using TicketFlow.Application.Dtos.Auth;

namespace TicketFlow.Application.Validators.Auth;

public class RevokeRequestValidator : AbstractValidator<RevokeRequest>
{
    public RevokeRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
