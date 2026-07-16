using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstaSafe.Infrastructure.Persistence.Configurations;

public class WebhookEventLogConfiguration : IEntityTypeConfiguration<WebhookEventLog>
{
    public void Configure(EntityTypeBuilder<WebhookEventLog> builder)
    {
        builder.Property(w => w.Source).HasMaxLength(50).IsRequired();
        builder.Property(w => w.EventType).HasMaxLength(100);
        builder.Property(w => w.RawPayload).IsRequired();
        builder.Property(w => w.AlatPayTransactionId).HasMaxLength(200);
        builder.Property(w => w.ProcessingResult).HasConversion<string>();

        builder.HasIndex(w => w.AlatPayTransactionId);
        builder.HasIndex(w => w.IsProcessed);
        builder.HasIndex(w => w.ReceivedAt);
    }
}
