using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Currency.RecordExchangeRate;
using Domain.Currency;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Currency;

/// <summary>sfc B0.2.3 / D-CURRENCY: POST dealer/exchange-rates records an append-only rate for the caller's dealer.</summary>
public class RecordExchangeRateCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static RecordExchangeRateCommandHandler CreateHandler(TestApplicationDbContext context, bool hasTenant = true)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(hasTenant);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        var user = new Mock<IUserContext>();
        user.SetupGet(u => u.UserId).Returns(ActorId);
        return new RecordExchangeRateCommandHandler(context, user.Object, tenant.Object);
    }

    [Fact]
    public async Task Handle_Should_RecordTheRateForTheCallersDealer()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context).Handle(
            new RecordExchangeRateCommand("usd", "ars", 1050.5m, T0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var saved = await context.DealerExchangeRates.AsNoTracking().SingleAsync();
        saved.Id.Should().Be(result.Value);
        saved.DealerId.Should().Be(DealerId);
        saved.BaseCurrency.Should().Be("USD");
        saved.QuoteCurrency.Should().Be("ARS");
        saved.Rate.Should().Be(1050.5m);
        saved.CreatedBy.Should().Be(ActorId);
    }

    [Fact]
    public async Task Handle_Should_RejectAnUnsupportedCurrency_WithoutPersisting()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context).Handle(
            new RecordExchangeRateCommand("EUR", "ARS", 1200m, T0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Unsupported");
        (await context.DealerExchangeRates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_RejectASameCurrencyPair()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context).Handle(
            new RecordExchangeRateCommand("ARS", "ARS", 1m, T0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.SamePair");
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenThereIsNoTenant()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context, hasTenant: false).Handle(
            new RecordExchangeRateCommand("USD", "ARS", 1000m, T0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
