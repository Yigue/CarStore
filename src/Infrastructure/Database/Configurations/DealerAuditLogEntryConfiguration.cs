using Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc B2.2: tenant-scoped audit table. No FK to the audited entities: the log outlives them.</summary>
internal sealed class DealerAuditLogEntryConfiguration : IEntityTypeConfiguration<DealerAuditLogEntry>
{
    public void Configure(EntityTypeBuilder<DealerAuditLogEntry> builder)
    {
        builder.ToTable("dealer_audit_logs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DealerId).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityId).IsRequired();
        builder.Property(e => e.Property).HasMaxLength(100).IsRequired();
        builder.Property(e => e.OldValue).HasMaxLength(DealerAuditLogEntry.MaxValueLength);
        builder.Property(e => e.NewValue).HasMaxLength(DealerAuditLogEntry.MaxValueLength);
        builder.Property(e => e.ActorUserId).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(DealerAuditLogEntry.MaxValueLength);
        builder.Property(e => e.CorrelationId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.OccurredAtUtc).IsRequired();

        builder.Ignore(e => e.DomainEvents);

        builder.HasIndex(e => new { e.DealerId, e.OccurredAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_dealer_audit_logs_dealer_id_occurred_at_utc");
        builder.HasIndex(e => new { e.DealerId, e.EntityType, e.OccurredAtUtc })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_dealer_audit_logs_dealer_id_entity_type_occurred_at_utc");
    }
}
