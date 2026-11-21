using FluentValidation;
using TicketFlow.Application.Dtos.Orders;

namespace TicketFlow.Application.Validators.Orders;

public class OrderSearchQueryValidator : AbstractValidator<OrderSearchQuery>
{
    public OrderSearchQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
