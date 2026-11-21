using FluentValidation;
using TicketFlow.Application.Dtos.Orders;

namespace TicketFlow.Application.Validators.Orders;

public class ConfirmOrderRequestValidator : AbstractValidator<ConfirmOrderRequest>
{
    public ConfirmOrderRequestValidator()
    {
        RuleFor(x => x.SeatId).NotEmpty();
    }
}
