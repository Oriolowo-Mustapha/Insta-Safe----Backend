using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Queries.GetOrderByReference;

public class GetOrderByReferenceQueryHandler : IRequestHandler<GetOrderByReferenceQuery, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IMapper _mapper;

    public GetOrderByReferenceQueryHandler(IOrderRepository orders, IMapper mapper)
    {
        _orders = orders; _mapper = mapper;
    }

    public async Task<Result<OrderDto>> Handle(GetOrderByReferenceQuery req, CancellationToken ct)
    {
        var reference = (req.Reference ?? "").Trim();
        Domain.Entities.Order? order = null;
        if (Guid.TryParse(reference, out var id))
            order = await _orders.GetByIdAsync(id, ct);
        order ??= await _orders.GetByOrderNumberAsync(reference.ToUpperInvariant(), ct);
        order ??= await _orders.GetByPaystackRefAsync(reference, ct);
        if (order is null)
            return Result<OrderDto>.Failure("Order not found.");
        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
