using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence.Configurations.ValueObjects;

/// <summary>
/// <see cref="MoneyValueConverter"/> for columns that may hold no amount at all.
///
/// <para>
/// The non-nullable converter cannot be reused here: EF applies it to the underlying value only,
/// so mapping a <c>Money?</c> through it turns "no amount was recorded" into <c>0</c> — and a
/// sale with no seña would read back as a sale with a seña of zero. Two different facts.
/// Rounding matches its sibling so a nullable column and a required one never disagree on cents.
/// </para>
/// </summary>
public class NullableMoneyValueConverter : ValueConverter<Money?, decimal?>
{
    public NullableMoneyValueConverter() : base(
        money => money == null ? null : Math.Round(money.Amount, 2, MidpointRounding.ToEven),
        amount => amount == null ? null : new Money(Math.Round(amount.Value, 2, MidpointRounding.ToEven), "ARS"))
    {
    }
}
