using System.Linq.Expressions;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence.Configurations.ValueObjects;

/// <summary>
/// Maps a <see cref="Money"/> property to TWO columns: the amount (keeps its historical column
/// name) and its currency (ADR C1, saas-functional-completeness B0.1).
///
/// <para>
/// This replaces the old <c>MoneyValueConverter</c>, which persisted only the decimal and rebuilt
/// every <see cref="Money"/> as "ARS" on read — a USD amount silently became ARS. A converter
/// cannot fix that: it sees one column at a time. Money is therefore mapped as an owned value
/// object (same technique already used for <c>Car.PurchaseCost</c> and
/// <c>ReconditioningTask.Cost</c>), so amount and currency always travel together.
/// </para>
/// </summary>
internal static class MoneyMapping
{
    public const int CurrencyLength = 3;

    /// <summary>Same rounding the old converter applied on write and read: 2 decimals, banker's.</summary>
    private static readonly ValueConverter<decimal, decimal> AmountRounding = new(
        amount => Math.Round(amount, 2, MidpointRounding.ToEven),
        amount => Math.Round(amount, 2, MidpointRounding.ToEven));

    /// <summary>A Money that is always present (NOT NULL amount and currency).</summary>
    public static void OwnsRequiredMoney<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Money?>> navigation,
        string amountColumn,
        string currencyColumn,
        bool precision18x2 = false)
        where TEntity : class
    {
        builder.OwnsOne(navigation, money => Configure(money, amountColumn, currencyColumn, precision18x2));
        builder.Navigation(navigation).IsRequired();
    }

    /// <summary>
    /// A Money that may be absent. "No amount was recorded" must stay distinct from "an amount of
    /// zero": both columns are NULL together, and the owned reference materializes as null.
    /// </summary>
    public static void OwnsOptionalMoney<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Money?>> navigation,
        string amountColumn,
        string currencyColumn)
        where TEntity : class
    {
        builder.OwnsOne(navigation, money => Configure(money, amountColumn, currencyColumn, false));
        builder.Navigation(navigation).IsRequired(false);
    }

    private static void Configure<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder<TEntity, Money> money,
        string amountColumn,
        string currencyColumn,
        bool precision18x2)
        where TEntity : class
    {
        var amount = money.Property(m => m.Amount)
            .HasColumnName(amountColumn)
            .HasConversion(AmountRounding);

        if (precision18x2)
        {
            amount.HasPrecision(18, 2);
        }

        money.Property(m => m.Currency)
            .HasColumnName(currencyColumn)
            .HasMaxLength(CurrencyLength);
    }
}
