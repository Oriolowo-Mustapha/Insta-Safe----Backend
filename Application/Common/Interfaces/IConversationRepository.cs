using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IConversationRepository
{
    Task<ConversationState?> GetByPhoneAsync(string phone, CancellationToken ct);
    Task AddAsync(ConversationState state, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);

    Task<List<SavedOrderDraft>> ListDraftsAsync(string phone, DraftTicketStatus status, CancellationToken ct);
    Task<SavedOrderDraft?> GetDraftAsync(Guid id, CancellationToken ct);
    Task AddDraftAsync(SavedOrderDraft draft, CancellationToken ct);
    Task RemoveDraftAsync(SavedOrderDraft draft, CancellationToken ct);
}
