using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Admin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Admin.Queries.ListAuditLog;

public sealed record ListAuditLogQuery(string? Action = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<List<AdminAuditDto>>>;

public class ListAuditLogQueryHandler : IRequestHandler<ListAuditLogQuery, Result<List<AdminAuditDto>>>
{
    private readonly IAppDbContext _db;

    public ListAuditLogQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<List<AdminAuditDto>>> Handle(ListAuditLogQuery req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 200);
        var q = _db.AdminAuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Action))
            q = q.Where(a => a.Action == req.Action.Trim());
        var rows = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Result<List<AdminAuditDto>>.Success(rows
            .Select(a => new AdminAuditDto(
                a.Id, a.Actor, a.Action, a.TargetType, a.TargetId, a.Note, a.CreatedAt))
            .ToList());
    }
}
