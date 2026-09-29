using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;

/// <summary>
/// Step 1 of vendor signup (web): minimal profile + password.
/// Bank details are NOT collected here — payout setup happens after
/// email verification via PUT payout (which completes onboarding).
/// Triggers a 6-digit email verification code.
/// </summary>
public sealed record RegisterVendorCommand(
    string Phone,
    string DisplayName,
    string FirstName,
    string LastName,
    string Email,
    string Password
) : IRequest<Result<VendorDto>>;
