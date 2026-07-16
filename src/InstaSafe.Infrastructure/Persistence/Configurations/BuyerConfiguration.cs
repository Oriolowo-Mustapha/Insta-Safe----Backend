using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstaSafe.Infrastructure.Persistence.Configurations;

public class BuyerConfiguration : IEntityTypeConfiguration<Buyer>
{
    public void Configure(EntityTypeBuilder<Buyer> builder)
    {
        builder.Property(b => b.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(b => b.LastName).HasMaxLength(100).IsRequired();
        builder.Property(b => b.Email).HasMaxLength(256).IsRequired();
        builder.Property(b => b.Phone).HasMaxLength(20).IsRequired();
    }
}
