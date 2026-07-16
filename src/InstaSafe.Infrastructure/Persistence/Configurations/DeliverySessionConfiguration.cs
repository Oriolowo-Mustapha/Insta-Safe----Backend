using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstaSafe.Infrastructure.Persistence.Configurations;

public class DeliverySessionConfiguration : IEntityTypeConfiguration<DeliverySession>
{
    public void Configure(EntityTypeBuilder<DeliverySession> builder)
    {
        builder.HasIndex(d => d.SessionId).IsUnique();
        builder.Property(d => d.Status).HasConversion<string>();
        builder.Property(d => d.PickupDeviceFingerprint).HasMaxLength(500);
        builder.Property(d => d.DeliveryDeviceFingerprint).HasMaxLength(500);
        builder.Property(d => d.FailureReason).HasConversion<string>();

        builder.HasOne(d => d.Order)
            .WithOne(o => o.DeliverySession)
            .HasForeignKey<DeliverySession>(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
