using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPayout;

public sealed record UpdateVendorPayoutCommand(Guid VendorId, string AccountNumber, string BankCode)
    : IRequest<Result<VendorDto>>;
