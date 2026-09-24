using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Queries.GetVendorOrders;

public sealed record GetVendorOrdersQuery(Guid VendorId, int Page = 1, int PageSize = 20)
    : IRequest<Result<List<OrderDto>>>;
