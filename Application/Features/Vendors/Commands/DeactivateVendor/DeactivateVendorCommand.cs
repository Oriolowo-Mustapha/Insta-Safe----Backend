using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.DeactivateVendor;

public sealed record DeactivateVendorCommand(Guid VendorId) : IRequest<Result<VendorDto>>;
