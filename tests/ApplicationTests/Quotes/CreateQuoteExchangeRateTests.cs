using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Quotes.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Currency;
using Domain.Quotes.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Quotes;

/// <summary>
/// sfc B0.2.3 / ADR C2 / RN (vehicle-currency-pricing): a quote in a currency different from the vehicle's requires a
/// manual rate loaded by the dealer, snapshotted into the quote. Without a currency the quote uses the vehicle's.
/// </summary>
public class CreateQuoteExchangeRateTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherDealerId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateQuoteCommandHandler Handler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return new CreateQuoteCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now }, tenant.Object);
    }

    private static async Task<(Car Car, Client Client)> SeedAsync(TestApplicationDbContext context, CurrencyCode carCurrency)
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2021, "FXR001", "desc", 15000m, Now.AddDays(-10), priceCurrency: carCurrency);
        var client = new Client(DealerId, "Eve", "Black", "999", "eve@test.com", "444", "Addr", Now.AddDays(-10));
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return (car, client);
    }

    private static DealerExchangeRate Rate(Guid dealerId, decimal value, DateTime effectiveAt) =>
        DealerExchangeRate.Record(dealerId, CurrencyCode.Usd, CurrencyCode.Ars, value, effectiveAt, ActorId).Value;

    private static CreateQuoteCommand Command(Guid carId, Guid clientId, string? currency) =>
        new(carId, clientId, null, 14000m, PaymentMethod.Contado, Now.AddDays(7), "c", Currency: currency);

    [Fact]
    public async Task Handle_Should_RequireARate_WhenTheQuoteCurrencyDiffersFromTheCar()
    {
        using var context = CreateContext();
        var (car, client) = await SeedAsync(context, CurrencyCode.Ars);

        var result = await Handler(context).Handle(Command(car.Id, client.Id, "USD"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.ExchangeRateRequired");
        (await context.Quotes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_SnapshotTheLatestEffectiveRate_IgnoringFutureAndOtherDealerRates()
    {
        using var context = CreateContext();
        var (car, client) = await SeedAsync(context, CurrencyCode.Ars);
        var older = Rate(DealerId, 1000m, Now.AddDays(-5));
        var current = Rate(DealerId, 1100m, Now.AddDays(-1));
        context.DealerExchangeRates.AddRange(older, current,
            Rate(DealerId, 1500m, Now.AddDays(2)),
            Rate(OtherDealerId, 9999m, Now.AddHours(-1)));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(Command(car.Id, client.Id, "USD"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var quote = await context.Quotes.SingleAsync(q => q.Id == result.Value);
        quote.ProposedPrice.Currency.Should().Be("USD");
        quote.ExchangeRate.Should().NotBeNull();
        quote.ExchangeRate!.Rate.Should().Be(1100m);
        quote.ExchangeRate.SourceRateId.Should().Be(current.Id);
        quote.ExchangeRate.CapturedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_Should_UseTheCarCurrency_AndSkipTheSnapshot_WhenNoCurrencyIsSent()
    {
        using var context = CreateContext();
        var (car, client) = await SeedAsync(context, CurrencyCode.Usd);

        var result = await Handler(context).Handle(Command(car.Id, client.Id, currency: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var quote = await context.Quotes.SingleAsync(q => q.Id == result.Value);
        quote.ProposedPrice.Currency.Should().Be("USD");
        quote.ExchangeRate.Should().BeNull();
    }
}
