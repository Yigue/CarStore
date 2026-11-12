using Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>
/// Tombstone of a purged tenant. Deliberately NOT tenant-filtered and without a foreign key to the dealer:
/// it must outlive the data it describes.
/// </summary>
internal sealed class TenantPurgeRecordConfiguration : IEntityTypeConfiguration<TenantPurgeRecord>
{
    public void Configure(EntityTypeBuilder<TenantPurgeRecord> builder)
    {
        builder.ToTable("tenant_purge_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.DealerId).IsRequired();
        builder.Property(r => r.DealerName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.PurgedAtUtc).IsRequired();
        builder.Property(r => r.CountsJson).IsRequired();

        builder.Ignore(r => r.Counts);
        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.DealerId).HasDatabaseName("ix_tenant_purge_records_dealer_id");
    }
}
