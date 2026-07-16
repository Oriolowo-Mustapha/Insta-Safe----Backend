using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstaSafe.Infrastructure.Persistence.Configurations;

public class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.Property(m => m.BusinessName).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(m => m.Email).IsUnique();
        builder.Property(m => m.Phone).HasMaxLength(20).IsRequired();
        builder.Property(m => m.CommissionRate).HasPrecision(5, 4);
        builder.Property(m => m.AlatPayBusinessId).HasMaxLength(100);
        builder.Property(m => m.PayoutBankAccount).HasMaxLength(20);
        builder.Property(m => m.PayoutBankCode).HasMaxLength(10);
    }
}
