using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstaSafe.Infrastructure.Persistence.Configurations;

public class PayoutSplitConfiguration : IEntityTypeConfiguration<PayoutSplit>
{
    public void Configure(EntityTypeBuilder<PayoutSplit> builder)
    {
        builder.Property(p => p.TotalAmount).HasPrecision(18, 2);
        builder.Property(p => p.MerchantAmount).HasPrecision(18, 2);
        builder.Property(p => p.PlatformCommission).HasPrecision(18, 2);
        builder.Property(p => p.CommissionRate).HasPrecision(5, 4);
        builder.Property(p => p.Status).HasConversion<string>();
        builder.Property(p => p.AlatPayPayoutReference).HasMaxLength(200);

        builder.HasOne(p => p.Order)
            .WithOne(o => o.PayoutSplit)
            .HasForeignKey<PayoutSplit>(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
