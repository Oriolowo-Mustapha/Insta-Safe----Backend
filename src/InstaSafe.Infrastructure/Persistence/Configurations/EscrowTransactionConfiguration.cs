using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstaSafe.Infrastructure.Persistence.Configurations;

public class EscrowTransactionConfiguration : IEntityTypeConfiguration<EscrowTransaction>
{
    public void Configure(EntityTypeBuilder<EscrowTransaction> builder)
    {
        builder.Property(e => e.AlatPayTransactionId).HasMaxLength(200);
        
        // Indexes
        builder.HasIndex(e => e.AlatPayTransactionId).IsUnique().HasFilter("\"AlatPayTransactionId\" IS NOT NULL");
        
        builder.Property(e => e.TransactionReference).HasMaxLength(200);
        // This makes sure internal system refs are unique if provided
        builder.HasIndex(e => e.TransactionReference).IsUnique().HasFilter("\"TransactionReference\" IS NOT NULL");
        
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(5);
        builder.Property(e => e.Channel).HasConversion<string>();
        builder.Property(e => e.Status).HasConversion<string>();
        builder.Property(e => e.VirtualAccountNumber).HasMaxLength(20);
        builder.Property(e => e.VirtualBankCode).HasMaxLength(10);

        builder.HasOne(e => e.Order)
            .WithOne(o => o.EscrowTransaction)
            .HasForeignKey<EscrowTransaction>(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
