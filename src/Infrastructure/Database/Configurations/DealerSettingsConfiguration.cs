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

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.Property(s => s.LastAssignedAgentIndex)
            .HasDefaultValue(0)
            .IsRequired();

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
