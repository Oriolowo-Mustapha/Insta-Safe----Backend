using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IConversationRepository
{
    Task<ConversationState?> GetByPhoneAsync(string phone, CancellationToken ct);
    Task AddAsync(ConversationState state, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
