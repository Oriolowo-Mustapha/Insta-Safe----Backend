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
}
