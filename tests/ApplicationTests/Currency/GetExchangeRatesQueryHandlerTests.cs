using Application.Abstractions.Tenancy;
using Application.Currency.GetExchangeRates;
using Domain.Currency;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Currency;

/// <summary>sfc B0.2.3: GET dealer/exchange-rates lists only the caller's dealer, most recent effective rate first.</summary>
public class GetExchangeRatesQueryHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherDealerId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static GetExchangeRatesQueryHandler CreateHandler(TestApplicationDbContext context, bool hasTenant = true)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(hasTenant);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return new GetExchangeRatesQueryHandler(context, tenant.Object);
    }

    private static DealerExchangeRate Rate(Guid dealerId, decimal value, DateTime effectiveAt) =>
        DealerExchangeRate.Record(dealerId, CurrencyCode.Usd, CurrencyCode.Ars, value, effectiveAt, ActorId).Value;

    [Fact]
    public async Task Handle_Should_ReturnOnlyTheCallersDealer_NewestEffectiveFirst()
    {
        using var context = CreateContext();
        var older = Rate(DealerId, 1000m, T0);
        var newer = Rate(DealerId, 1100m, T0.AddDays(2));
        context.DealerExchangeRates.AddRange(older, newer, Rate(OtherDealerId, 9m, T0.AddDays(5)));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new GetExchangeRatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(r => r.Rate).Should().Equal(1100m, 1000m);
        result.Value.Select(r => r.Id).Should().Equal(newer.Id, older.Id);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenThereIsNoTenant()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context, hasTenant: false).Handle(new GetExchangeRatesQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
