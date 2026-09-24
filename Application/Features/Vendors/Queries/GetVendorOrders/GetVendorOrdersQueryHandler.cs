using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Vendors.Queries.GetVendorOrders;

public class GetVendorOrdersQueryHandler : IRequestHandler<GetVendorOrdersQuery, Result<List<OrderDto>>>
{
    private readonly IVendorRepository _vendors;
    private readonly IOrderRepository _orders;
    private readonly IMapper _mapper;

    public GetVendorOrdersQueryHandler(
        IVendorRepository vendors, IOrderRepository orders, IMapper mapper)
    {
        _vendors = vendors; _orders = orders; _mapper = mapper;
    }

    public async Task<Result<List<OrderDto>>> Handle(GetVendorOrdersQuery req, CancellationToken ct)
    {
        var vendor = await _vendors.GetByIdAsync(req.VendorId, ct);
        if (vendor is null)
            return Result<List<OrderDto>>.Failure("Vendor not found.");

        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 100);
        var orders = await _orders.ListByVendorAsync(vendor.Id, vendor.Phone, page, size, ct);
        return Result<List<OrderDto>>.Success(_mapper.Map<List<OrderDto>>(orders));
    }
}
