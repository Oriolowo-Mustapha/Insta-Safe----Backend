namespace InstaSafe.Application.Features.Vendors.DTOs;

public sealed record VendorDto(
    Guid Id,
    string Phone,
    string DisplayName,
    string? AccountNumber,
    string? BankCode,
    string? PaystackRecipientCode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    bool EmailVerified = false,
    bool OnboardingCompleted = false);
