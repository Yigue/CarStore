using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Infrastructure.Database.Configurations;

internal sealed class DealerSettingsConfiguration : IEntityTypeConfiguration<DealerSettingsEntity>
{
    public void Configure(EntityTypeBuilder<DealerSettingsEntity> builder)
    {
        builder.ToTable("dealer_settings");

        builder.HasKey(s => s.Id);

        // Una fila por dealer.
        builder.HasIndex(s => s.DealerId).IsUnique();

        // Subdomain uniqueness — DB is the source of truth (REQ: concurrent
        // provisioning cannot race past an app-level check). PostgreSQL partial
        // unique index ignores rows with NULL HostName so legacy seed rows
        // without a subdomain continue to work.
        builder.HasIndex(s => s.HostName)
            .IsUnique()
            .HasFilter("\"HostName\" IS NOT NULL")
            .HasDatabaseName("IX_DealerSettings_HostName_Unique");

        builder.Property(s => s.DealerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.ContactEmail)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.NotificationsEnabled)
            .HasDefaultValue(true);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.Property(s => s.LastAssignedAgentIndex)
            .HasDefaultValue(0)
            .IsRequired();

        // Platform suspension columns
        builder.Property(s => s.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(s => s.IsActive)
            .HasDatabaseName("ix_dealer_settings_is_active");

        builder.Property(s => s.SuspendedAt)
            .IsRequired(false);

        builder.Property(s => s.SuspendReason)
            .HasMaxLength(500)
            .IsRequired(false);

        // RowVersion is intentionally NOT mapped here.
        // For Postgres: ApplicationDbContext.OnModelCreating maps it to the xmin system column
        // (concurrency token) in the Postgres-only block.
        // For InMemory / SQLite (tests): it is ignored so no extra column is created.
        // RowVersion will be 0 for all test entities (accepted; ETag = "v0").
        builder.Ignore(s => s.RowVersion);

        // Visual settings
        builder.Property(s => s.LogoUrl)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(s => s.PrimaryColor)
            .HasMaxLength(7)
            .IsRequired(false);

        builder.Property(s => s.SecondaryColor)
            .HasMaxLength(7)
            .IsRequired(false);

        builder.Property(s => s.FooterText)
            .HasMaxLength(200)
            .IsRequired(false);
    }
}
