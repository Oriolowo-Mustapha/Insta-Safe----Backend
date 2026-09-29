using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Dispatch.DTOs;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Admin.Queries.ListAllDispatchers;

public sealed record ListAllDispatchersQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<List<DispatcherDto>>>;

public class ListAllDispatchersQueryHandler : IRequestHandler<ListAllDispatchersQuery, Result<List<DispatcherDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;

    public ListAllDispatchersQueryHandler(IAppDbContext db, IMapper mapper)
    {
        _db = db; _mapper = mapper;
    }

    public async Task<Result<List<DispatcherDto>>> Handle(ListAllDispatchersQuery req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 100);
        var rows = await _db.Dispatchers.AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Result<List<DispatcherDto>>.Success(_mapper.Map<List<DispatcherDto>>(rows));
    }
}
