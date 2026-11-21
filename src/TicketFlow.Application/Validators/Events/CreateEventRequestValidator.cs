using FluentValidation;
using TicketFlow.Application.Dtos.Events;

namespace TicketFlow.Application.Validators.Events;

public class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .Length(3, 200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Venue)
            .NotEmpty()
            .Length(2, 200);

        RuleFor(x => x.StartsAtUtc)
            .Must(startsAt => startsAt > timeProvider.GetUtcNow())
            .WithMessage("startsAtUtc must be in the future.");
    }
}
