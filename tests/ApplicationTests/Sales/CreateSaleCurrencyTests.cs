using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Sales.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Currency;
using Domain.Financial.Attributes;
using Domain.Quotes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuotePaymentMethod = Domain.Quotes.Attributes.PaymentMethod;

namespace Application.UnitTests.Sales;

/// <summary>
/// sfc B0.2.3 / ADR C2: the sale currency is explicit, else the accepted quote's, else the vehicle's. A currency
/// different from the vehicle's needs a manual rate snapshot; a sale converted from a quote inherits the quote's
/// snapshot and never recalculates it.
/// </summary>
public class CreateSaleCurrencyTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateSaleCommandHandler Handler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return new CreateSaleCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now }, tenant.Object);
    }

    private static async Task<(Car Car, Client Client)> SeedAsync(TestApplicationDbContext context, CurrencyCode carCurrency)
    {
        var marca = new Marca("Ford");
        var modelo = new Modelo("Focus", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2020, "SLC001", "desc", 10000m, Now.AddDays(-10), priceCurrency: carCurrency);
        var client = new Client(DealerId, "Alice", "Johnson", "30111222", "alice@test.com", "111", "Addr1", Now.AddDays(-10));
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return (car, client);
    }

    private static DealerExchangeRate Rate(decimal value, DateTime effectiveAt) =>
        DealerExchangeRate.Record(DealerId, CurrencyCode.Usd, CurrencyCode.Ars, value, effectiveAt, ActorId).Value;

    private static CreateSaleCommand Command(Guid carId, Guid clientId, string? currency, Guid? quoteId = null, decimal? price = 9000m) =>
        new(carId, clientId, price, PaymentMethod.Cash, "CN-CUR", "c", QuoteId: quoteId, Currency: currency);

    [Fact]
    public async Task Handle_Should_UseTheCarCurrency_WhenNoneIsSent()
    {
        using var context = CreateContext();
        var (car, client) = await SeedAsync(context, CurrencyCode.Usd);

        var result = await Handler(context).Handle(Command(car.Id, client.Id, currency: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.SingleAsync(s => s.Id == result.Value);
        sale.FinalPrice.Currency.Should().Be("USD");
        sale.ExchangeRate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_RequireARate_WhenTheSaleCurrencyDiffersFromTheCar()
    {
        using var context = CreateContext();
        var (car, client) = await SeedAsync(context, CurrencyCode.Ars);

        var result = await Handler(context).Handle(Command(car.Id, client.Id, "USD"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.ExchangeRateRequired");
        (await context.Sales.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_SnapshotTheLatestEffectiveRate()
    {
        using var context = CreateContext();
        var (car, client) = await SeedAsync(context, CurrencyCode.Ars);
        var current = Rate(1100m, Now.AddDays(-1));
        context.DealerExchangeRates.AddRange(Rate(1000m, Now.AddDays(-5)), current, Rate(1500m, Now.AddDays(3)));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(Command(car.Id, client.Id, "USD"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.SingleAsync(s => s.Id == result.Value);
        sale.FinalPrice.Currency.Should().Be("USD");
        sale.ExchangeRate!.SourceRateId.Should().Be(current.Id);
        sale.ExchangeRate.Rate.Should().Be(1100m);
    }

    [Fact]
    public async Task Handle_Should_InheritTheQuoteCurrencyAndSnapshot_WithoutRecalculating()
    {
        using var context = CreateContext();
        var (car, client) = await SeedAsync(context, CurrencyCode.Ars);
        var quoteSnapshot = new ExchangeRateSnapshot("USD", "ARS", 980m, Now.AddDays(-3), Guid.NewGuid());
        var quote = new Quote(DealerId, car, client, null, 9000m, QuotePaymentMethod.Contado, Now.AddDays(20), "q",
            Now.AddDays(-3), CurrencyCode.Usd, quoteSnapshot);
        quote.Accept(Now.AddDays(-2));
        context.Quotes.Add(quote);
        // A newer rate exists, but the sale must keep what the quote agreed.
        context.DealerExchangeRates.Add(Rate(1300m, Now.AddDays(-1)));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(
            Command(car.Id, client.Id, currency: null, quoteId: quote.Id, price: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.SingleAsync(s => s.Id == result.Value);
        sale.FinalPrice.Should().Be(new Money(9000m, "USD"));
        sale.ExchangeRate.Should().Be(quoteSnapshot);
    }

    [Fact]
    public async Task Handle_Should_RejectAnUnsupportedCurrency()
    {
        using var context = CreateContext();
        var (car, client) = await SeedAsync(context, CurrencyCode.Ars);

        var result = await Handler(context).Handle(Command(car.Id, client.Id, "EUR"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Unsupported");
    }
}
