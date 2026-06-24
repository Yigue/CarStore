using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class BackfillAuditConfiguration : IEntityTypeConfiguration<BackfillAudit>
{
    public void Configure(EntityTypeBuilder<BackfillAudit> builder)
    {
        builder.ToTable("backfill_audit");

        builder.HasKey(ba => ba.Id);

        builder.Property(ba => ba.ActorUserId)
            .IsRequired();

        builder.Property(ba => ba.Action)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(ba => ba.AffectedRowCount)
            .IsRequired();

        builder.Property(ba => ba.ExecutionTimeMs)
            .IsRequired(false);

        builder.Property(ba => ba.MetadataJson)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(ba => ba.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Per-tenant lookup is the dominant access pattern ("show me the audit log for dealer X").
        builder.HasIndex(ba => new { ba.DealerId, ba.CreatedAtUtc })
            .HasDatabaseName("ix_backfill_audit_dealer_created");

        // Filter audit by actor when investigating a specific user.
        builder.HasIndex(ba => ba.ActorUserId)
            .HasDatabaseName("ix_backfill_audit_actor");
    }
}
