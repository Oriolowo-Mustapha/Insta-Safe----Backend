using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Merchant> Merchants { get; }
    DbSet<Buyer> Buyers { get; }
    DbSet<Order> Orders { get; }
    DbSet<EscrowTransaction> EscrowTransactions { get; }
    DbSet<DeliverySession> DeliverySessions { get; }
    DbSet<PayoutSplit> PayoutSplits { get; }
    DbSet<Dispute> Disputes { get; }
    DbSet<WebhookEventLog> WebhookEventLogs { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<InAppNotification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    DbSet<T> Set<T>() where T : class;
}
