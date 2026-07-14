using Application.Abstractions.Tenancy;
using Application.Financing.Commands.CalculateFinancing;
using Microsoft.EntityFrameworkCore;
using Moq;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests.Financing;

/// <summary>
/// SDD Parte 1 (financing-simulator-fix) — TDD RED→GREEN for
/// CalculateFinancingCommandHandler.
///
/// Requisito 1 de la spec:
/// - TnaOverride presente (porcentaje entero) debe normalizarse a fracción
///   (72 -> 0.72) antes de invocar el servicio de dominio.
/// - TnaOverride nulo/0 debe resolver DealerSettings.InterestRateTna del
///   tenant actual vía ICurrentTenantService y normalizarlo de la misma forma.
/// </summary>
public sealed class CalculateFinancingCommandHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("cccc0000-0000-0000-0000-000000000003");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options, TestDealerId);
    }

    private static Mock<ICurrentTenantService> MockTenant(Guid dealerId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.Setup(t => t.DealerId).Returns(dealerId);
        mock.Setup(t => t.HasTenant).Returns(true);
        return mock;
    }

    [Fact]
    public async Task Handle_WithTnaOverride_NormalizesPercentageToFractionBeforeCalculating()
    {
        // Arrange — dealer has its own configured rate, but request overrides it.
        using var context = CreateContext();
        context.DealerSettings.Add(new DealerSettingsEntity(
            TestDealerId, "Test Dealer", "test@test.com", interestRateTna: 50.00m));
        await context.SaveChangesAsync();

        var tenant = MockTenant(TestDealerId);
        var handler = new CalculateFinancingCommandHandler(
            tenant.Object, context, new Domain.Services.FinancingCalculationService());

        // Act — 72 (whole percentage) must become 0.72 (fraction) before Calculate()
        var result = await handler.Handle(
            new CalculateFinancingCommand(1_000_000m, 24, TnaOverride: 72m),
            CancellationToken.None);

        // Assert — finite, positive result (would be Infinity/NaN if unnormalized)
        result.IsSuccess.Should().BeTrue();
        double.IsFinite((double)result.Value.MonthlyPayment).Should().BeTrue();
        result.Value.MonthlyPayment.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task Handle_WithoutTnaOverride_UsesDealerSettingsInterestRateTna_Normalized()
    {
        // Arrange — no override; the dealer's configured 65.50% (whole percentage)
        // must be normalized to 0.655 fraction before Calculate().
        using var context = CreateContext();
        context.DealerSettings.Add(new DealerSettingsEntity(
            TestDealerId, "Test Dealer", "test@test.com", interestRateTna: 65.50m));
        await context.SaveChangesAsync();

        var tenant = MockTenant(TestDealerId);
        var handler = new CalculateFinancingCommandHandler(
            tenant.Object, context, new Domain.Services.FinancingCalculationService());

        // Act
        var withDealerRate = await handler.Handle(
            new CalculateFinancingCommand(1_000_000m, 24, TnaOverride: null),
            CancellationToken.None);

        var withExplicitFraction = new Domain.Services.FinancingCalculationService()
            .CalculateFrenchAmortization(1_000_000m, 0.6550m, 24);

        // Assert — dealer rate normalized (65.50 -> 0.6550) matches manual fraction calc
        withDealerRate.IsSuccess.Should().BeTrue();
        withDealerRate.Value.MonthlyPayment.Should().Be(withExplicitFraction.MonthlyPayment);
    }

    [Fact]
    public async Task Handle_WithZeroTnaOverride_FallsBackToDealerSettings()
    {
        // Arrange — TnaOverride = 0 must be treated as "not provided" per spec.
        using var context = CreateContext();
        context.DealerSettings.Add(new DealerSettingsEntity(
            TestDealerId, "Test Dealer", "test@test.com", interestRateTna: 65.50m));
        await context.SaveChangesAsync();

        var tenant = MockTenant(TestDealerId);
        var handler = new CalculateFinancingCommandHandler(
            tenant.Object, context, new Domain.Services.FinancingCalculationService());

        // Act
        var result = await handler.Handle(
            new CalculateFinancingCommand(1_000_000m, 24, TnaOverride: 0m),
            CancellationToken.None);

        var expected = new Domain.Services.FinancingCalculationService()
            .CalculateFrenchAmortization(1_000_000m, 0.6550m, 24);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MonthlyPayment.Should().Be(expected.MonthlyPayment);
    }
}
