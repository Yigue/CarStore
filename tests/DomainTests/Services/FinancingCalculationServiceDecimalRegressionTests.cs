using Domain.Services;

namespace DomainTests.Services;

/// <summary>
/// B2.1 (RN-026/RN-027): the French amortization moves from double to pure decimal.
/// Reference values were captured from the previous double-based implementation.
/// </summary>
public class FinancingCalculationServiceDecimalRegressionTests
{
    private const decimal Tolerance = 0.01m;

    private readonly FinancingCalculationService _sut = new();

    // principal, annualRate, installments, monthlyPayment, totalWithInterest, cft, tea (double implementation)
    public static TheoryData<decimal, decimal, int, decimal, decimal, decimal, decimal> ReferenceCases => new()
    {
        { 1_000_000m, 0.72m, 24, 98447.07m, 2362729.77m, 101.22m, 72.00m },
        { 1_000_000m, 0.655m, 24, 90545.37m, 2173088.98m, 89.22m, 65.50m },
        { 500_000m, 0.40m, 12, 53330.88m, 639970.55m, 48.21m, 40.00m },
        { 2_500_000m, 0.95m, 60, 311720.29m, 18703217.38m, 149.50m, 95.00m },
        { 350_000m, 0.30m, 6, 64339.72m, 386038.34m, 34.49m, 30.00m },
        { 10_000_000m, 1.20m, 72, 1782036.92m, 128306658.51m, 213.84m, 120.00m },
        { 800_000m, 0.55m, 36, 54291.71m, 1954501.58m, 71.22m, 55.00m },
    };

    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public void CalculateFrenchAmortization_MatchesPreviousDoubleResults_WithinTolerance(
        decimal principal, decimal rate, int installments,
        decimal monthlyPayment, decimal total, decimal cft, decimal tea)
    {
        var result = _sut.CalculateFrenchAmortization(principal, rate, installments);

        result.MonthlyPayment.Should().BeApproximately(monthlyPayment, Tolerance);
        result.TotalWithInterest.Should().BeApproximately(total, Tolerance);
        result.CFT.Should().BeApproximately(cft, Tolerance);
        result.TEA.Should().BeApproximately(tea, Tolerance);
    }

    [Theory]
    [InlineData("1.06", 12, "2.0121964800")]
    [InlineData("2", 10, "1024")]
    [InlineData("5", 0, "1")]
    [InlineData("1.5", 1, "1.5")]
    public void DecimalMath_PowInt_ComputesIntegerPowers(string baseValue, int exponent, string expected)
    {
        var result = DecimalMath.PowInt(decimal.Parse(baseValue, System.Globalization.CultureInfo.InvariantCulture), exponent);

        result.Should().BeApproximately(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), 0.0000001m);
    }

    [Fact]
    public void DecimalMath_PowInt_RejectsNegativeExponent()
    {
        var act = () => DecimalMath.PowInt(2m, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
