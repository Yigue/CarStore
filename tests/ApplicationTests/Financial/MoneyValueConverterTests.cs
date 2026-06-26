using System;
using Domain.Shared.ValueObjects;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Xunit;

namespace Application.UnitTests.Financial;

public class MoneyValueConverterTests
{
    [Fact]
    public void ConvertToProvider_RoundTrip_MoneyReadBackAsArs()
    {
        // Arrange
        var converter = new MoneyValueConverter();
        var convertTo = (Func<Money, decimal>)converter.ConvertToProviderExpression.Compile();
        var convertFrom = (Func<decimal, Money>)converter.ConvertFromProviderExpression.Compile();

        var originalMoney = new Money(1234.56m, "ARS");

        // Act
        var dbValue = convertTo(originalMoney);
        var materializedMoney = convertFrom(dbValue);

        // Assert
        Assert.Equal(1234.56m, dbValue);
        Assert.Equal(1234.56m, materializedMoney.Amount);
        Assert.Equal("ARS", materializedMoney.Currency);
    }

    [Fact]
    public void ConvertToProvider_RoundsTo2DecimalPlaces()
    {
        // Arrange
        var converter = new MoneyValueConverter();
        var convertTo = (Func<Money, decimal>)converter.ConvertToProviderExpression.Compile();
        var convertFrom = (Func<decimal, Money>)converter.ConvertFromProviderExpression.Compile();

        var originalMoney = new Money(1234.5678m, "ARS");

        // Act
        var dbValue = convertTo(originalMoney);
        var materializedMoney = convertFrom(dbValue);

        // Assert
        Assert.Equal(1234.57m, dbValue);
        Assert.Equal(1234.57m, materializedMoney.Amount);
        Assert.Equal("ARS", materializedMoney.Currency);
    }
}
