using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Admin.Queries.ListDisputes;

public sealed record ListDisputesQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<List<OrderDto>>>;

public class ListDisputesQueryHandler : IRequestHandler<ListDisputesQuery, Result<List<OrderDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;

    public ListDisputesQueryHandler(IAppDbContext db, IMapper mapper)
    {
        _db = db; _mapper = mapper;
    }

    public async Task<Result<List<OrderDto>>> Handle(ListDisputesQuery req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 100);
        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Disputed)
            .OrderBy(o => o.UpdatedAt ?? o.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Result<List<OrderDto>>.Success(_mapper.Map<List<OrderDto>>(orders));
    }
}
