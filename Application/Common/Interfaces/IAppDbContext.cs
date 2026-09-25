using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<Vendor> Vendors { get; }
    DbSet<Dispatcher> Dispatchers { get; }
    DbSet<ConversationState> ConversationStates { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<EscrowLedger> Ledgers { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<WebhookEvent> WebhookEvents { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
