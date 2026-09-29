using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Admin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Admin.Queries.SearchWebhooks;

public sealed record SearchWebhooksQuery(
    string? Provider = null, string? Event = null, bool? ValidOnly = null,
    int Page = 1, int PageSize = 50) : IRequest<Result<List<WebhookEventDto>>>;

public class SearchWebhooksQueryHandler : IRequestHandler<SearchWebhooksQuery, Result<List<WebhookEventDto>>>
{
    private readonly IAppDbContext _db;

    public SearchWebhooksQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<List<WebhookEventDto>>> Handle(SearchWebhooksQuery req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 200);
        var q = _db.WebhookEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Provider))
            q = q.Where(w => w.Provider == req.Provider.Trim());
        if (!string.IsNullOrWhiteSpace(req.Event))
            q = q.Where(w => w.EventType == req.Event.Trim());
        if (req.ValidOnly == true)
            q = q.Where(w => w.SignatureValid);
        var rows = await q.OrderByDescending(w => w.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Result<List<WebhookEventDto>>.Success(rows
            .Select(w => new WebhookEventDto(
                w.Id, w.Provider, w.EventType, w.SignatureValid, w.Processed,
                w.IdempotencyKey, w.CreatedAt))
            .ToList());
    }
}
