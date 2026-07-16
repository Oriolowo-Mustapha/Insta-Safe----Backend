using System.Reflection;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Common;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly IMediator _mediator;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IMediator mediator) : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Buyer> Buyers => Set<Buyer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<EscrowTransaction> EscrowTransactions => Set<EscrowTransaction>();
    public DbSet<DeliverySession> DeliverySessions => Set<DeliverySession>();
    public DbSet<PayoutSplit> PayoutSplits => Set<PayoutSplit>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<WebhookEventLog> WebhookEventLogs => Set<WebhookEventLog>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<InAppNotification> Notifications => Set<InAppNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.LastModifiedAt = DateTime.UtcNow;
                    break;
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);
        await DispatchDomainEventsAsync();

        return result;
    }

    private async Task DispatchDomainEventsAsync()
    {
        var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .ToArray();

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToArray();

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent);
        }
    }
}
