using Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>Retry list of storage keys whose delete failed. Not tenant-filtered, no FK: it must outlive the dealer.</summary>
internal sealed class PendingStorageDeletionConfiguration : IEntityTypeConfiguration<PendingStorageDeletion>
{
    public void Configure(EntityTypeBuilder<PendingStorageDeletion> builder)
    {
        builder.ToTable("pending_storage_deletions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.DealerId).IsRequired();
        builder.Property(p => p.ObjectKey).HasMaxLength(1024).IsRequired();
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.Attempts).IsRequired();
        builder.Property(p => p.LastError).HasMaxLength(PendingStorageDeletion.MaxErrorLength);

        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => p.DealerId).HasDatabaseName("ix_pending_storage_deletions_dealer_id");
    }
}
