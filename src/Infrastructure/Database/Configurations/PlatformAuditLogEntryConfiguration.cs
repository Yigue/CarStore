using Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class PlatformAuditLogEntryConfiguration : IEntityTypeConfiguration<PlatformAuditLogEntry>
{
    public void Configure(EntityTypeBuilder<PlatformAuditLogEntry> builder)
    {
        builder.ToTable("platform_audit_logs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DealerId).IsRequired();
        builder.Property(e => e.DealerSettingsId).IsRequired();
        builder.Property(e => e.DealerName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(e => e.ActorKind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.ActorUserId).IsRequired();
        builder.Property(e => e.ActorEmail).HasMaxLength(320);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.Property(e => e.RecordedAtUtc).IsRequired();
        builder.Property(e => e.SourceEventKey).HasMaxLength(200).IsRequired();

        builder.HasIndex(e => e.SourceEventKey).IsUnique()
            .HasDatabaseName(PlatformAuditLogEntry.SourceEventKeyUniqueIndex);
        builder.HasIndex(e => e.OccurredAtUtc).IsDescending();
        builder.HasIndex(e => new { e.DealerId, e.OccurredAtUtc }).IsDescending(false, true);
        builder.HasIndex(e => new { e.Action, e.OccurredAtUtc }).IsDescending(false, true);
    }
}
