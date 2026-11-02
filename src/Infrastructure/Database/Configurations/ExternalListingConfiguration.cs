using Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc C1.2 / ADR C25: one mirrored provider listing per (dealer, provider, car).</summary>
internal sealed class ExternalListingConfiguration : IEntityTypeConfiguration<ExternalListing>
{
    public void Configure(EntityTypeBuilder<ExternalListing> builder)
    {
        builder.ToTable("external_listings");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.DealerId).IsRequired();
        builder.Property(l => l.CarId).IsRequired();
        builder.Property(l => l.Provider).HasMaxLength(DealerIntegration.MaxProviderLength).IsRequired();
        builder.Property(l => l.ExternalId).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(l => l.LastError).HasMaxLength(1000);
        builder.Ignore(l => l.DomainEvents);

        builder.HasIndex(l => new { l.DealerId, l.Provider, l.CarId })
            .IsUnique()
            .HasDatabaseName("ux_external_listings_dealer_id_provider_car_id");

        builder.HasIndex(l => new { l.Provider, l.ExternalId })
            .IsUnique()
            .HasDatabaseName("ux_external_listings_provider_external_id");
    }
}
