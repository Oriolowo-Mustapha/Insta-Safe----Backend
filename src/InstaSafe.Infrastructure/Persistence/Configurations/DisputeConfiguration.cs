using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstaSafe.Infrastructure.Persistence.Configurations;

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.Property(d => d.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(d => d.EvidenceUrls).HasMaxLength(4000);
        builder.Property(d => d.Status).HasConversion<string>();
        builder.Property(d => d.Resolution).HasMaxLength(2000);

        builder.HasOne(d => d.Order)
            .WithOne(o => o.Dispute)
            .HasForeignKey<Dispute>(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(d => d.Buyer)
            .WithMany()
            .HasForeignKey(d => d.RaisedByBuyerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
