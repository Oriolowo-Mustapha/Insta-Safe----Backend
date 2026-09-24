using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Queries.GetOrderById;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IMapper _mapper;

    public GetOrderByIdQueryHandler(IOrderRepository orders, IMapper mapper)
    {
        _orders = orders; _mapper = mapper;
    }

    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<OrderDto>.Failure("Order not found.");
        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
