using System.Linq.Expressions;
using Domain.Currency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.ValueObjects;

/// <summary>
/// sfc B0.2 / ADR C2: maps an optional <see cref="ExchangeRateSnapshot"/> as owned columns on the operation's own table
/// (quote, deposit, sale). All columns are null when the operation is in the vehicle's currency.
/// </summary>
internal static class ExchangeRateSnapshotMapping
{
    public static void OwnsOptionalExchangeRate<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, ExchangeRateSnapshot?>> navigation)
        where TEntity : class
    {
        builder.OwnsOne(navigation, rate =>
        {
            rate.Property(r => r.Base).HasColumnName("exchange_rate_base").HasMaxLength(3).IsFixedLength();
            rate.Property(r => r.Quote).HasColumnName("exchange_rate_quote").HasMaxLength(3).IsFixedLength();
            rate.Property(r => r.Rate).HasColumnName("exchange_rate").HasPrecision(18, 6);
            rate.Property(r => r.CapturedAtUtc).HasColumnName("exchange_rate_captured_at_utc");
            rate.Property(r => r.SourceRateId).HasColumnName("exchange_rate_source_id");
        });

        builder.Navigation(navigation).IsRequired(false);
    }
}
