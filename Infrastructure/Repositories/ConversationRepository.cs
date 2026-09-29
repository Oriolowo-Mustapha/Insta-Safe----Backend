using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using InstaSafe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly AppDbContext _db;
    public ConversationRepository(AppDbContext db) => _db = db;

    public Task<ConversationState?> GetByPhoneAsync(string phone, CancellationToken ct)
        => _db.ConversationStates.FirstOrDefaultAsync(s => s.Phone == phone, ct)!;

    public async Task AddAsync(ConversationState state, CancellationToken ct)
        => await _db.ConversationStates.AddAsync(state, ct);

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public Task<List<SavedOrderDraft>> ListDraftsAsync(string phone, DraftTicketStatus status, CancellationToken ct)
        => _db.SavedOrderDrafts.AsNoTracking()
            .Where(d => d.VendorPhone == phone && d.Status == status)
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .ToListAsync(ct);

    public Task<SavedOrderDraft?> GetDraftAsync(Guid id, CancellationToken ct)
        => _db.SavedOrderDrafts.FirstOrDefaultAsync(d => d.Id == id, ct)!;

    public async Task AddDraftAsync(SavedOrderDraft draft, CancellationToken ct)
        => await _db.SavedOrderDrafts.AddAsync(draft, ct);

    public Task RemoveDraftAsync(SavedOrderDraft draft, CancellationToken ct)
    {
        _db.SavedOrderDrafts.Remove(draft);
        return Task.CompletedTask;
    }
}
