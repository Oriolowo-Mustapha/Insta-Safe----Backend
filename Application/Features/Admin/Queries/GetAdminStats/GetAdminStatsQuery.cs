using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Admin.DTOs;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Admin.Queries.GetAdminStats;

public sealed record GetAdminStatsQuery : IRequest<Result<AdminStatsDto>>;

public class GetAdminStatsQueryHandler : IRequestHandler<GetAdminStatsQuery, Result<AdminStatsDto>>
{
    private readonly IAppDbContext _db;

    public GetAdminStatsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<AdminStatsDto>> Handle(GetAdminStatsQuery req, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var dayAgo = now.AddDays(-1);

        var ordersByStatus = await _db.Orders.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = (long)g.Count() })
            .ToListAsync(ct);

        var heldGmv = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Held)
            .SumAsync(o => (long?)o.AmountKobo, ct) ?? 0;

        var releasedToday = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Released && o.ReleasedAt >= now.Date)
            .SumAsync(o => (long?)o.AmountKobo, ct) ?? 0;

        return Result<AdminStatsDto>.Success(new AdminStatsDto(
            VendorTotal: await _db.Vendors.CountAsync(ct),
            VendorActive: await _db.Vendors.CountAsync(v => v.IsActive, ct),
            OrdersByStatus: ordersByStatus.ToDictionary(x => x.Status.ToString(), x => x.Count),
            HeldGmvKobo: heldGmv,
            ReleasedTodayKobo: releasedToday,
            OpenDisputes: await _db.Orders.CountAsync(o => o.Status == OrderStatus.Disputed, ct),
            OpenDraftTickets: await _db.SavedOrderDrafts
                .CountAsync(d => d.Status == Domain.Entities.DraftTicketStatus.Open, ct),
            FailedWebhooks24h: await _db.WebhookEvents
                .CountAsync(w => w.CreatedAt >= dayAgo && (!w.SignatureValid || !w.Processed), ct),
            OutboxBacklog: await _db.OutboxMessages
                .CountAsync(m => m.PublishedAt == null && m.Attempts < 5, ct)));
    }
}
