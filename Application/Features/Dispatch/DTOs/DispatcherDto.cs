namespace InstaSafe.Application.Features.Dispatch.DTOs;

public sealed record DispatcherDto(
    Guid Id,
    string Phone,
    bool IsActive,
    DateTimeOffset CreatedAt);
