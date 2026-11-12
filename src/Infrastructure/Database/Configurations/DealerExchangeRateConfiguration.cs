using Domain.Currency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc B0.2.2 / ADR C2: append-only manual exchange rates, tenant-scoped by <c>DealerId</c>.</summary>
internal sealed class DealerExchangeRateConfiguration : IEntityTypeConfiguration<DealerExchangeRate>
{
    public void Configure(EntityTypeBuilder<DealerExchangeRate> builder)
    {
        builder.ToTable("dealer_exchange_rates");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.DealerId).IsRequired();
        builder.Property(r => r.BaseCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(r => r.QuoteCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(r => r.Rate).HasPrecision(18, 6).IsRequired();
        builder.Property(r => r.EffectiveAtUtc).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired();

        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => new { r.DealerId, r.BaseCurrency, r.QuoteCurrency, r.EffectiveAtUtc })
            .HasDatabaseName("ix_dealer_exchange_rates_dealer_pair_effective");
    }
}
