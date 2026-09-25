using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InstaSafe.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly IPublisher _publisher;

    public AppDbContext(DbContextOptions<AppDbContext> options, IPublisher publisher) : base(options)
    {
        _publisher = publisher;
    }

    // Parameterless ctor for design-time tools
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        _publisher = null!;
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Dispatcher> Dispatchers => Set<Dispatcher>();
    public DbSet<ConversationState> ConversationStates => Set<ConversationState>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<EscrowLedger> Ledgers => Set<EscrowLedger>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Vendor>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(120);
            e.Property(x => x.LastName).HasMaxLength(120);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.AccountNumber).HasMaxLength(20);
            e.Property(x => x.BankCode).HasMaxLength(10);
            e.Property(x => x.PaystackRecipientCode).HasMaxLength(100);
            e.Property(x => x.OtpHash).HasMaxLength(200);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
            e.Property(x => x.EmailOtpHash).HasMaxLength(200);
            e.HasIndex(x => x.Phone).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<ConversationState>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.DraftJson).HasMaxLength(2000);
            e.HasIndex(x => x.Phone).IsUnique();
        });

        b.Entity<ChatMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.Body).HasMaxLength(1000).IsRequired();
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<Dispatcher>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.OtpHash).HasMaxLength(200);
            e.HasIndex(x => x.Phone).IsUnique();
        });

        b.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.DriverPhone).HasMaxLength(20);
            e.Property(x => x.DriverAccountNumber).HasMaxLength(20);
            e.Property(x => x.DriverBankCode).HasMaxLength(10);
            e.Property(x => x.DriverRecipientCode).HasMaxLength(100);
            e.Property(x => x.DriverTransferReference).HasMaxLength(100);
            e.Property(x => x.DisputeReason).HasMaxLength(1000);
            e.HasIndex(x => x.VendorId);
            e.HasIndex(x => x.DriverId);
            e.HasIndex(x => x.ReleaseDueAt);
            e.Property(x => x.VendorPhone).HasMaxLength(20).IsRequired();
            e.Property(x => x.CustomerName).HasMaxLength(120).IsRequired();
            e.Property(x => x.CustomerPhone).HasMaxLength(20).IsRequired();
            e.Property(x => x.BuyerEmail).HasMaxLength(200);
            e.Property(x => x.DeliveryAddress).HasMaxLength(500).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.PaystackReference).HasMaxLength(100);
            e.Property(x => x.PaystackAuthUrl).HasMaxLength(1000);
            e.Property(x => x.VendorRecipientCode).HasMaxLength(100);
            e.Property(x => x.RefundReference).HasMaxLength(100);
            e.Property(x => x.PaystackCustomerCode).HasMaxLength(100);
            e.Property(x => x.PayVirtualAccountNumber).HasMaxLength(20);
            e.Property(x => x.PayVirtualAccountBank).HasMaxLength(120);
            e.Property(x => x.PayVirtualAccountName).HasMaxLength(200);
            e.OwnsMany(x => x.Items, ib =>
            {
                ib.ToJson();
            });
            e.HasIndex(x => x.PaystackReference).IsUnique();
        });

        b.Entity<EscrowLedger>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PublishedAt);
        });

        b.Entity<WebhookEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.IdempotencyKey).HasMaxLength(300);
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var domainEntities = ChangeTracker.Entries<Domain.Common.BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        var result = await base.SaveChangesAsync(ct);

        // Publish in-process domain events after commit
        if (_publisher is not null)
        {
            foreach (var entry in domainEntities)
            {
                foreach (var evt in entry.Entity.DomainEvents)
                    await _publisher.Publish(evt, ct);
                entry.Entity.ClearDomainEvents();
            }
        }
        else
        {
            foreach (var entry in domainEntities) entry.Entity.ClearDomainEvents();
        }

        return result;
    }
}
