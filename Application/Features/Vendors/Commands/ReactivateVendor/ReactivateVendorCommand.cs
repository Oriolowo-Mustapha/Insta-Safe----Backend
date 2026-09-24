using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.ReactivateVendor;

public sealed record ReactivateVendorCommand(Guid VendorId) : IRequest<Result<VendorDto>>;
