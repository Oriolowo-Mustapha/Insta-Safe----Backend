using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Admin.Queries.ListAllOrders;

public sealed record ListAllOrdersQuery(OrderStatus? Status = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<List<OrderDto>>>;

public class ListAllOrdersQueryHandler : IRequestHandler<ListAllOrdersQuery, Result<List<OrderDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;

    public ListAllOrdersQueryHandler(IAppDbContext db, IMapper mapper)
    {
        _db = db; _mapper = mapper;
    }

    public async Task<Result<List<OrderDto>>> Handle(ListAllOrdersQuery req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 100);
        var q = _db.Orders.AsNoTracking().AsQueryable();
        if (req.Status.HasValue) q = q.Where(o => o.Status == req.Status.Value);
        var orders = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Result<List<OrderDto>>.Success(_mapper.Map<List<OrderDto>>(orders));
    }
}
