using Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Constants;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AggregateType)
            .HasMaxLength(100);

        // Composite index for activity-timeline queries filtered by (Type, OccurredOnUtc)
        builder.HasIndex(x => new { x.Type, x.OccurredOnUtc })
            .HasDatabaseName("ix_outbox_type_occurred");

        // Index for tenant-scoped queries on AggregateType + DealerId
        builder.HasIndex(x => new { x.AggregateType, x.DealerId })
            .HasDatabaseName("ix_outbox_dealer");
    }
}
