using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Queries.GetVendorById;

public sealed record GetVendorByIdQuery(Guid VendorId) : IRequest<Result<VendorDto>>;
