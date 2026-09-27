using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Admin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Admin.Queries.GetOutboxStatus;

public sealed record GetOutboxStatusQuery : IRequest<Result<OutboxStatusDto>>;

public class GetOutboxStatusQueryHandler : IRequestHandler<GetOutboxStatusQuery, Result<OutboxStatusDto>>
{
    private readonly IAppDbContext _db;

    public GetOutboxStatusQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<OutboxStatusDto>> Handle(GetOutboxStatusQuery req, CancellationToken ct)
    {
        var errors = await _db.OutboxMessages.AsNoTracking()
            .Where(m => m.Attempts >= 5 || m.Error != null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        return Result<OutboxStatusDto>.Success(new OutboxStatusDto(
            Backlog: await _db.OutboxMessages
                .CountAsync(m => m.PublishedAt == null && m.Attempts < 5, ct),
            RecentErrors: errors
                .Select(m => new OutboxErrorDto(m.Id, m.Type, m.Attempts, m.Error, m.CreatedAt))
                .ToList()));
    }
}
