using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc B4.3 / RN-018 / ADR C7: one row per (dealer, kind). The real allocation path
/// (DealerSequenceAllocator on a relational provider) bypasses the change tracker entirely via
/// raw SQL — this mapping only backs the InMemory test-provider fallback and the AddQuoteNumbers
/// migration backfill.</summary>
internal sealed class DealerSequenceConfiguration : IEntityTypeConfiguration<DealerSequence>
{
    public void Configure(EntityTypeBuilder<DealerSequence> builder)
    {
        builder.ToTable("dealer_sequences");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.DealerId).IsRequired();
        builder.Property(s => s.Kind).HasMaxLength(50).IsRequired();
        builder.Property(s => s.NextValue).IsRequired();

        builder.Ignore(s => s.DomainEvents);

        builder.HasIndex(s => new { s.DealerId, s.Kind })
            .IsUnique()
            .HasDatabaseName("ux_dealer_sequences_dealer_id_kind");
    }
}
