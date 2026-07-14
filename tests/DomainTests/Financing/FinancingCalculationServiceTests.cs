using Domain.Services;

namespace DomainTests.Financing;

/// <summary>
/// SDD Parte 1 (financing-simulator-fix) — TDD RED→GREEN for guard clauses
/// on <see cref="FinancingCalculationService.CalculateFrenchAmortization"/>.
///
/// Requisito 2 de la spec: la tasa (annualRate) que llega a este servicio ya
/// debe venir normalizada como fracción (0.72, no 72) — la normalización vive
/// en el handler. Este servicio solo debe validar límites y no desbordar.
/// </summary>
public class FinancingCalculationServiceTests
{
    private readonly FinancingCalculationService _sut = new();

    [Fact]
    public void CalculateFrenchAmortization_WithValidRate_ReturnsFiniteAndPositiveResult()
    {
        // 65.5% TNA normalizada a fracción (0.655), 24 cuotas
        var result = _sut.CalculateFrenchAmortization(1_000_000m, 0.655m, 24);

        result.MonthlyPayment.Should().BeGreaterThan(0m);
        double.IsFinite((double)result.MonthlyPayment).Should().BeTrue();
        double.IsFinite((double)result.TotalWithInterest).Should().BeTrue();
    }

    [Fact]
    public void CalculateFrenchAmortization_WithRealReportedCase_72PercentTna24Installments_DoesNotProduceInfinity()
    {
        // Caso real reportado en el bug: 72% TNA normalizado a 0.72 fracción, 24 cuotas
        var result = _sut.CalculateFrenchAmortization(1_000_000m, 0.72m, 24);

        double.IsFinite((double)result.MonthlyPayment).Should().BeTrue();
        double.IsNaN((double)result.MonthlyPayment).Should().BeFalse();
        result.MonthlyPayment.Should().BeGreaterThan(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    public void CalculateFrenchAmortization_WithNonPositiveRate_ThrowsDomainException(decimal invalidRate)
    {
        var act = () => _sut.CalculateFrenchAmortization(1_000_000m, invalidRate, 24);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculateFrenchAmortization_WithNonPositiveInstallments_ThrowsDomainException(int invalidInstallments)
    {
        var act = () => _sut.CalculateFrenchAmortization(1_000_000m, 0.655m, invalidInstallments);

        act.Should().Throw<DomainException>();
    }
}
