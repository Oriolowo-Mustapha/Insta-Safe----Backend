using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Orders.Queries.GetOrderTimeline;

public record GetOrderTimelineQuery(Guid OrderId) : IRequest<Result<List<TimelineEntry>>>;

public class TimelineEntry
{
    public string Event { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string? Detail { get; init; }
}
