using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Admin.Queries.ListAllVendors;

public sealed record ListAllVendorsQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<Result<List<VendorDto>>>;

public class ListAllVendorsQueryHandler : IRequestHandler<ListAllVendorsQuery, Result<List<VendorDto>>>
{
    private readonly IAppDbContext _db;
    private readonly IMapper _mapper;

    public ListAllVendorsQueryHandler(IAppDbContext db, IMapper mapper)
    {
        _db = db; _mapper = mapper;
    }

    public async Task<Result<List<VendorDto>>> Handle(ListAllVendorsQuery req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 100);
        var q = _db.Vendors.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim().ToLower();
            q = q.Where(v => v.Phone.ToLower().Contains(s)
                || v.DisplayName.ToLower().Contains(s)
                || (v.Email != null && v.Email.ToLower().Contains(s)));
        }
        var vendors = await q.OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Result<List<VendorDto>>.Success(_mapper.Map<List<VendorDto>>(vendors));
    }
}
