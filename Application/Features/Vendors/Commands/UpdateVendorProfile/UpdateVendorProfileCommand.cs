using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.UpdateVendorProfile;

public sealed record UpdateVendorProfileCommand(Guid VendorId, string DisplayName) : IRequest<Result<VendorDto>>;
