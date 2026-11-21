using FluentValidation;
using TicketFlow.Application.Dtos.Seats;

namespace TicketFlow.Application.Validators.Seats;

public class SeatSearchQueryValidator : AbstractValidator<SeatSearchQuery>
{
    public SeatSearchQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);
    }
}
