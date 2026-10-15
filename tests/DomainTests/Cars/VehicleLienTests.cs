using Domain.Cars;
using Domain.Shared.ValueObjects;
using FluentAssertions;

namespace DomainTests.Cars;

/// <summary>sfc B7.2 / RN-037: a lien (prenda) on a vehicle is Active until it is explicitly liquidated, once.</summary>
public class VehicleLienTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_Should_StartActive_AndLiquidateOnce()
    {
        var lien = VehicleLien.Register(Guid.NewGuid(), Guid.NewGuid(), "Banco Norte", Money.Of(500_000m, CurrencyCode.Ars), "P-123", T0).Value;

        lien.Status.Should().Be(VehicleLienStatus.Active);
        lien.Liquidate(T0.AddDays(30)).IsSuccess.Should().BeTrue();
        lien.Status.Should().Be(VehicleLienStatus.Liquidated);
        lien.LiquidatedAtUtc.Should().Be(T0.AddDays(30));
        lien.Liquidate(T0.AddDays(31)).Error.Code.Should().Be("VehicleLien.AlreadyLiquidated");
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Banco", 0)]
    public void Register_Should_RequireACreditorAndAPositiveAmount(string creditor, decimal amount)
    {
        var result = VehicleLien.Register(Guid.NewGuid(), Guid.NewGuid(), creditor, Money.Of(amount, CurrencyCode.Ars), null, T0);

        result.IsFailure.Should().BeTrue();
    }
}
