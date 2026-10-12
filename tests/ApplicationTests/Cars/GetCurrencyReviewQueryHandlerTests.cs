using Application.Abstractions.Tenancy;
using Application.Cars.CurrencyReview;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Cars;

/// <summary>
/// sfc B0.1.5: GET dealer/currency-review lists the caller's cars whose purchase cost is recorded in a currency
/// different from the price, so an operator can fix them before B0.2 starts relying on the currency.
/// </summary>
public class GetCurrencyReviewQueryHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherDealerId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static GetCurrencyReviewQueryHandler CreateHandler(TestApplicationDbContext context, bool hasTenant = true)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(hasTenant);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return new GetCurrencyReviewQueryHandler(context, tenant.Object);
    }

    private static Car NewCar(Guid dealerId, string patente)
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        return new Car(dealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1800, 0, 2024, patente, "desc", 25000m, DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_Should_ListOnlyCarsWhosePurchaseCostCurrencyDiffersFromThePrice()
    {
        using var context = CreateContext();
        var mismatched = NewCar(DealerId, "MIS001");
        mismatched.SetPurchaseCost(Money.Of(1000m, CurrencyCode.Usd), DateTime.UtcNow);
        var sameCurrency = NewCar(DealerId, "SAM001");
        sameCurrency.SetPurchaseCost(Money.Of(1000m, CurrencyCode.Ars), DateTime.UtcNow);
        var noPurchaseCost = NewCar(DealerId, "NOP001");
        var otherDealer = NewCar(OtherDealerId, "OTH001");
        otherDealer.SetPurchaseCost(Money.Of(1000m, CurrencyCode.Usd), DateTime.UtcNow);
        context.Cars.AddRange(mismatched, sameCurrency, noPurchaseCost, otherDealer);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new GetCurrencyReviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new CurrencyReviewItemResponse(
                mismatched.Id, "ARS", "USD", 25000m, 1000m));
    }

    [Fact]
    public async Task Handle_Should_ReturnEmpty_WhenEveryPurchaseCostMatchesThePrice()
    {
        using var context = CreateContext();
        var car = NewCar(DealerId, "OKK001");
        car.SetPurchaseCost(Money.Of(1000m, CurrencyCode.Ars), DateTime.UtcNow);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new GetCurrencyReviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenThereIsNoTenant()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context, hasTenant: false).Handle(new GetCurrencyReviewQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
