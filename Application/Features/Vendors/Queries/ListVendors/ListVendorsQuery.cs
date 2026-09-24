using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Queries.ListVendors;

public sealed record ListVendorsQuery(int Page = 1, int PageSize = 20, bool? ActiveOnly = true)
    : IRequest<Result<List<VendorDto>>>;
