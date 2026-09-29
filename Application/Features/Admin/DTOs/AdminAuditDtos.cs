using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Features.Admin.DTOs;

public sealed record ChatMessageDto(
    Guid Id, string Phone, ChatDirection Direction, string Body, DateTimeOffset CreatedAt);

public sealed record WebhookEventDto(
    Guid Id, string Provider, string EventType, bool SignatureValid, bool Processed,
    string? IdempotencyKey, DateTimeOffset CreatedAt);

public sealed record OutboxStatusDto(int Backlog, List<OutboxErrorDto> RecentErrors);

public sealed record OutboxErrorDto(Guid Id, string Type, int Attempts, string? Error, DateTimeOffset CreatedAt);

public sealed record AdminAuditDto(
    Guid Id, string Actor, string Action, string TargetType, string TargetId,
    string? Note, DateTimeOffset CreatedAt);
