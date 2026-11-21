using FluentValidation;
using TicketFlow.Application.Dtos.Events;

namespace TicketFlow.Application.Validators.Events;

public class BulkSeatRequestValidator : AbstractValidator<BulkSeatRequest>
{
    public BulkSeatRequestValidator()
    {
        RuleFor(x => x.Seats)
            .NotNull()
            .Must(seats => seats is not null && seats.Count is > 0 and <= 500)
            .WithMessage("Seats must contain between 1 and 500 items.");

        RuleFor(x => x.Seats)
            .Must(seats => seats is null || HasUniqueSeatCodes(seats))
            .WithMessage("Seat codes must be unique within the request.");

        RuleForEach(x => x.Seats)
            .SetValidator(new BulkSeatItemRequestValidator());
    }

    private static bool HasUniqueSeatCodes(IReadOnlyList<BulkSeatItemRequest> seats)
    {
        var normalized = seats
            .Select(s => string.IsNullOrWhiteSpace(s.SeatCode) ? string.Empty : s.SeatCode.Trim().ToUpperInvariant())
            .ToList();

        return normalized.Distinct().Count() == normalized.Count;
    }
}
