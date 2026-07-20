using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstaSafe.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.OrderReference).HasMaxLength(50).IsRequired();
        builder.HasIndex(o => o.OrderReference).IsUnique();
        builder.Property(o => o.ItemName).HasMaxLength(500).IsRequired();
        builder.Property(o => o.ItemDescription).HasMaxLength(2000);
        builder.Property(o => o.Price).HasPrecision(18, 2);
        builder.Property(o => o.Currency).HasMaxLength(5).HasDefaultValue("NGN");
        builder.Property(o => o.DispatcherPhone).HasMaxLength(20);
        builder.Property(o => o.Status).HasConversion<string>();
        builder.Property(o => o.RiskLevel).HasMaxLength(20);

        builder.HasOne(o => o.Merchant)
            .WithMany(m => m.Orders)
            .HasForeignKey(o => o.MerchantId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(o => o.Buyer)
            .WithMany(b => b.Orders)
            .HasForeignKey(o => o.BuyerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.MerchantId);
    }
}
