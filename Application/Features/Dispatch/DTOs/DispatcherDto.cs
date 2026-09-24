namespace InstaSafe.Application.Features.Dispatch.DTOs;

public sealed record DispatcherDto(
    Guid Id,
    string Phone,
    string? FirstName,
    string? LastName,
    string? AccountNumber,
    string? BankCode,
    string? PaystackRecipientCode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
