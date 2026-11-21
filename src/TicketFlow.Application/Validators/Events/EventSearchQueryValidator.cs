using FluentValidation;
using TicketFlow.Application.Dtos.Events;

namespace TicketFlow.Application.Validators.Events;

public class EventSearchQueryValidator : AbstractValidator<EventSearchQuery>
{
    private static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
    {
        "startsAtAsc",
        "startsAtDesc",
        "createdAtDesc",
    };

    public EventSearchQueryValidator()
    {
        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) || AllowedSorts.Contains(sortBy.Trim()))
            .WithMessage("sortBy must be one of: startsAtAsc, startsAtDesc, createdAtDesc.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.ToUtc)
            .Must((query, toUtc) => !query.FromUtc.HasValue || !toUtc.HasValue || query.FromUtc <= toUtc)
            .WithMessage("fromUtc must be less than or equal to toUtc.");
    }
}
