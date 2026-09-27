using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Admin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Features.Admin.Queries.SearchChats;

public sealed record SearchChatsQuery(
    string? Phone = null, DateTimeOffset? From = null, DateTimeOffset? To = null,
    int Page = 1, int PageSize = 50) : IRequest<Result<List<ChatMessageDto>>>;

public class SearchChatsQueryHandler : IRequestHandler<SearchChatsQuery, Result<List<ChatMessageDto>>>
{
    private readonly IAppDbContext _db;

    public SearchChatsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<List<ChatMessageDto>>> Handle(SearchChatsQuery req, CancellationToken ct)
    {
        var page = req.Page <= 0 ? 1 : req.Page;
        var size = Math.Clamp(req.PageSize, 1, 200);
        var q = _db.ChatMessages.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Phone))
            q = q.Where(c => c.Phone == req.Phone.Trim());
        if (req.From.HasValue) q = q.Where(c => c.CreatedAt >= req.From.Value);
        if (req.To.HasValue) q = q.Where(c => c.CreatedAt <= req.To.Value);
        var rows = await q.OrderBy(c => c.CreatedAt)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Result<List<ChatMessageDto>>.Success(rows
            .Select(c => new ChatMessageDto(c.Id, c.Phone, c.Direction, c.Body, c.CreatedAt))
            .ToList());
    }
}
