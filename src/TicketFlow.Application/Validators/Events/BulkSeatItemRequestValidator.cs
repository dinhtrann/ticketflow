using FluentValidation;
using TicketFlow.Application.Dtos.Events;

namespace TicketFlow.Application.Validators.Events;

public class BulkSeatItemRequestValidator : AbstractValidator<BulkSeatItemRequest>
{
    public BulkSeatItemRequestValidator()
    {
        RuleFor(x => x.SeatCode)
            .NotEmpty()
            .Length(1, 20);

        RuleFor(x => x.Section)
            .NotEmpty()
            .Length(1, 50);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0);
    }
}
