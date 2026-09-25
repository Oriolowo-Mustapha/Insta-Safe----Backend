namespace InstaSafe.Application.Features.Orders.DTOs;

public sealed record TimelineEventDto(string Key, string Label, DateTimeOffset? At);

public sealed record OrderTimelineDto(
    Guid OrderId,
    string Reference,
    string Status,
    long AmountKobo,
    List<TimelineEventDto> Events);
