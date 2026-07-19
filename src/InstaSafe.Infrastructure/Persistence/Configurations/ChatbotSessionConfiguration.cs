using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstaSafe.Infrastructure.Persistence.Configurations;

public class ChatbotSessionConfiguration : IEntityTypeConfiguration<ChatbotSession>
{
    public void Configure(EntityTypeBuilder<ChatbotSession> builder)
    {
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(c => c.PhoneNumber).IsUnique();

        builder.Property(c => c.CurrentState)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(c => c.TemporaryData)
            .HasColumnType("jsonb");
    }
}
