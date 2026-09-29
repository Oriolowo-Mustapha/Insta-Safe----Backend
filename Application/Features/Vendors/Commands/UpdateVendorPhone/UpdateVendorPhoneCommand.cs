using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPhone;

public sealed record UpdateVendorPhoneCommand(Guid VendorId, string Phone) : IRequest<Result<VendorDto>>;
