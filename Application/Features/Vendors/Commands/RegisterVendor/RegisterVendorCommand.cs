using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;

/// <summary>
/// Step 1 of vendor signup (web): minimal profile + password.
/// Triggers a 6-digit email verification code. Use verify-email next,
/// then PUT payout to finish onboarding.
/// </summary>
public sealed record RegisterVendorCommand(
    string Phone,
    string DisplayName,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? AccountNumber = null,
    string? BankCode = null
) : IRequest<Result<VendorDto>>;
