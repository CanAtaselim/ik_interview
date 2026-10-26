using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Infrastructure.Outbox;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(m => m.Type).HasMaxLength(512);
        builder.Property(m => m.Payload).HasColumnType("jsonb");

        // İşleyici yalnızca bekleyen mesajları okur.
        builder.HasIndex(m => m.OccurredAt).HasFilter("processed_at is null");
    }
}
