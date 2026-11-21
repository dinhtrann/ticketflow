using FluentValidation;
using TicketFlow.Application.Dtos.Events;

namespace TicketFlow.Application.Validators.Events;

public class UpdateEventStatusRequestValidator : AbstractValidator<UpdateEventStatusRequest>
{
    public UpdateEventStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
