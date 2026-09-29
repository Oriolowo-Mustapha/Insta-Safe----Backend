using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Queries.GetOrderByReference;

public class GetOrderByReferenceQueryHandler : IRequestHandler<GetOrderByReferenceQuery, Result<PublicOrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IMapper _mapper;

    public GetOrderByReferenceQueryHandler(IOrderRepository orders, IMapper mapper)
    {
        _orders = orders; _mapper = mapper;
    }

    public async Task<Result<PublicOrderDto>> Handle(GetOrderByReferenceQuery req, CancellationToken ct)
    {
        var reference = (req.Reference ?? "").Trim();
        Domain.Entities.Order? order = null;
        if (Guid.TryParse(reference, out var id))
            order = await _orders.GetByIdAsync(id, ct);
        order ??= await _orders.GetByOrderNumberAsync(reference.ToUpperInvariant(), ct);
        order ??= await _orders.GetByPaystackRefAsync(reference, ct);
        if (order is null)
            return Result<PublicOrderDto>.Failure("Order not found.");
        return Result<PublicOrderDto>.Success(_mapper.Map<PublicOrderDto>(order));
    }
}
