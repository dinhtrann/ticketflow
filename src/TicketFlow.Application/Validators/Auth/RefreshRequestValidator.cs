using FluentValidation;
using TicketFlow.Application.Dtos.Auth;

namespace TicketFlow.Application.Validators.Auth;

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
