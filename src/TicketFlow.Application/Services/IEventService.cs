using TicketFlow.Application.Common;
using TicketFlow.Application.Dtos.Events;

namespace TicketFlow.Application.Services;

public interface IEventService
{
    Task<PagedResult<EventSummaryDto>> SearchAsync(EventSearchQuery query, bool isAdmin, CancellationToken ct = default);
    Task<EventDetailDto> GetByIdAsync(Guid eventId, bool isAdmin, CancellationToken ct = default);
    Task<EventDetailDto> CreateAsync(CreateEventRequest request, CancellationToken ct = default);
    Task<EventDetailDto> UpdateStatusAsync(Guid eventId, UpdateEventStatusRequest request, CancellationToken ct = default);
    Task<BulkSeatResponse> BulkCreateSeatsAsync(Guid eventId, BulkSeatRequest request, CancellationToken ct = default);
}
