using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Queries.GetVendorByPhone;

public sealed record GetVendorByPhoneQuery(string Phone) : IRequest<Result<VendorDto>>;
