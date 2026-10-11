using Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

internal sealed class TenantExportConfiguration : IEntityTypeConfiguration<TenantExport>
{
    public void Configure(EntityTypeBuilder<TenantExport> builder)
    {
        builder.ToTable("tenant_exports");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DealerId).IsRequired();
        builder.Property(e => e.RequestedBy).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.RequestedAtUtc).IsRequired();
        builder.Property(e => e.ObjectKey).HasMaxLength(300);
        builder.Property(e => e.Sha256).HasMaxLength(64);
        builder.Property(e => e.FailureReason).HasMaxLength(1000);

        builder.Ignore(e => e.IsActive);
        builder.Ignore(e => e.DomainEvents);

        // Serves "active export?" / "last export in 24 h?" per tenant and the expiry sweep.
        builder.HasIndex(e => new { e.DealerId, e.RequestedAtUtc })
            .HasDatabaseName("ix_tenant_exports_dealer_id_requested_at_utc");
        builder.HasIndex(e => new { e.Status, e.ExpiresAt })
            .HasDatabaseName("ix_tenant_exports_status_expires_at");
    }
}
