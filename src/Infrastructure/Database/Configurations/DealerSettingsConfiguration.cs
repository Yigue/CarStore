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
    }
}
