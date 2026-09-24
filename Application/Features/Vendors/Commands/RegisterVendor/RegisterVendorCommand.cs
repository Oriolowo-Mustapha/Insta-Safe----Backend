using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;

public sealed record RegisterVendorCommand(
    string Phone,
    string DisplayName,
    string? AccountNumber = null,
    string? BankCode = null,
    string? FirstName = null,
    string? LastName = null,
    string? Email = null
) : IRequest<Result<VendorDto>>;
