using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Queries.ListOrders;

public class ListOrdersQueryHandler : IRequestHandler<ListOrdersQuery, Result<List<OrderDto>>>
{
    private readonly IOrderRepository _orders;
    private readonly IMapper _mapper;

    public ListOrdersQueryHandler(IOrderRepository orders, IMapper mapper)
    {
        _orders = orders; _mapper = mapper;
    }

    public async Task<Result<List<OrderDto>>> Handle(ListOrdersQuery req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 100);
        var orders = await _orders.ListAsync(page, size, ct);
        return Result<List<OrderDto>>.Success(_mapper.Map<List<OrderDto>>(orders));
    }
}
