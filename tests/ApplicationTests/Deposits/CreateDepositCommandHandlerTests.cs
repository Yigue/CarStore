using Application.Abstractions.Caching;
using Application.Abstractions.Tenancy;
using Application.Deposits.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Currency;
using Domain.Deposits;
using Domain.Financial.Attributes;
using Domain.Quotes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuotePaymentMethod = Domain.Quotes.Attributes.PaymentMethod;

namespace Application.UnitTests.Deposits;

/// <summary>
/// sfc B1.3.5 / RN-001..RN-004: POST deposits. The car must be Disponible (or Reservado by the same quote), one Active
/// deposit per car, a different currency needs the dealer's manual rate, validity comes from the dealer settings, and the
/// money received is recorded as an income transaction.
/// </summary>
public class CreateDepositCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherDealerId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateDepositCommandHandler Handler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        var categories = new Mock<ICachedCategoryService>();
        return new CreateDepositCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now }, tenant.Object, categories.Object);
    }

    private static async Task<(Car Car, Client Client, Domain.DealerSettings.DealerSettings Dealer)> SeedAsync(
        TestApplicationDbContext context, Guid? carDealer = null, CurrencyCode? carCurrency = null)
    {
        var dealer = new Domain.DealerSettings.DealerSettings(DealerId, "Autos Sur", "sur@dealer.com");
        context.DealerSettings.Add(dealer);
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(carDealer ?? DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2021, "DEP001", "desc", 10000m, Now.AddDays(-30), priceCurrency: carCurrency);
        var client = new Client(DealerId, "Ana", "Perez", "30111222", "ana@buyer.com", "111", "Addr", Now.AddDays(-30));
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return (car, client, dealer);
    }

    private static CreateDepositCommand Command(Guid carId, Guid clientId, string currency = "ARS", Guid? quoteId = null) =>
        new(carId, clientId, null, quoteId, 500_000m, currency, PaymentMethod.Cash);

    [Fact]
    public async Task Handle_Should_CreateAnActiveDeposit_ReserveTheCar_AndRecordTheIncome()
    {
        using var context = CreateContext();
        var (car, client, _) = await SeedAsync(context);

        var result = await Handler(context).Handle(Command(car.Id, client.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var deposit = await context.Deposits.SingleAsync();
        deposit.Id.Should().Be(result.Value);
        deposit.Status.Should().Be(DepositStatus.Active);
        deposit.ExpiresAtUtc.Should().Be(Now.AddDays(Deposit.DefaultValidityDays));
        (await context.Cars.SingleAsync()).ServiceCar.Should().Be(StatusServiceCar.Reservado);
        var income = await context.Transactions.SingleAsync();
        income.Type.Should().Be(TransactionType.Income);
        income.Amount.Should().Be(new Money(500_000m, "ARS"));
        income.SourceId.Should().Be(deposit.Id);
    }

    [Fact]
    public async Task Handle_Should_Reject_ASecondActiveDeposit_OnTheSameCar()
    {
        using var context = CreateContext();
        var (car, client, _) = await SeedAsync(context);
        (await Handler(context).Handle(Command(car.Id, client.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var second = await Handler(context).Handle(Command(car.Id, client.Id), CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("DEPOSIT_ALREADY_ACTIVE");
    }

    [Fact]
    public async Task Handle_Should_RespectTheCarReservation_OfAnotherQuote_ButAcceptTheSameQuote()
    {
        using var context = CreateContext();
        var (car, client, _) = await SeedAsync(context);
        var quote = new Quote(DealerId, car, client, null, 10000m, QuotePaymentMethod.Contado, Now.AddDays(20), "q", Now.AddDays(-2));
        quote.Accept(Now.AddDays(-1));
        car.Reserve(Now.AddDays(-1));
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var withoutQuote = await Handler(context).Handle(Command(car.Id, client.Id), CancellationToken.None);
        var withQuote = await Handler(context).Handle(Command(car.Id, client.Id, quoteId: quote.Id), CancellationToken.None);

        withoutQuote.Error.Code.Should().Be("Deposits.CarNotAvailable");
        withQuote.IsSuccess.Should().BeTrue();
        (await context.Cars.SingleAsync()).ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }

    [Fact]
    public async Task Handle_Should_RequireARate_ForAnotherCurrency_AndSnapshotIt()
    {
        using var context = CreateContext();
        var (car, client, _) = await SeedAsync(context);

        var withoutRate = await Handler(context).Handle(Command(car.Id, client.Id, "USD"), CancellationToken.None);
        var rate = DealerExchangeRate.Record(DealerId, CurrencyCode.Usd, CurrencyCode.Ars, 1100m, Now.AddDays(-1), Guid.NewGuid()).Value;
        context.DealerExchangeRates.Add(rate);
        await context.SaveChangesAsync();
        var withRate = await Handler(context).Handle(Command(car.Id, client.Id, "USD"), CancellationToken.None);

        withoutRate.Error.Code.Should().Be("Currency.ExchangeRateRequired");
        withRate.IsSuccess.Should().BeTrue();
        (await context.Deposits.SingleAsync()).ExchangeRate!.SourceRateId.Should().Be(rate.Id);
    }

    [Fact]
    public async Task Handle_Should_CopyTheFrozenRate_OntoTheIncomeTransaction()
    {
        using var context = CreateContext();
        var (car, client, _) = await SeedAsync(context);
        var rate = DealerExchangeRate.Record(DealerId, CurrencyCode.Usd, CurrencyCode.Ars, 1100m, Now.AddDays(-1), Guid.NewGuid()).Value;
        context.DealerExchangeRates.Add(rate);
        await context.SaveChangesAsync();

        (await Handler(context).Handle(Command(car.Id, client.Id, "USD"), CancellationToken.None)).IsSuccess.Should().BeTrue();

        // sfc B0.3.1 / ADR C2: the income carries the deposit's own snapshot, not a later rate.
        var deposit = await context.Deposits.SingleAsync();
        var transaction = await context.Transactions.SingleAsync();
        transaction.ExchangeRate.Should().NotBeNull();
        transaction.ExchangeRate.Should().Be(deposit.ExchangeRate);
        transaction.ExchangeRate!.SourceRateId.Should().Be(rate.Id);
    }

    [Fact]
    public async Task Handle_Should_UseTheDealersConfiguredValidity()
    {
        using var context = CreateContext();
        var (car, client, dealer) = await SeedAsync(context);
        dealer.SetDepositValidityDays(3).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(Command(car.Id, client.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Deposits.SingleAsync()).ExpiresAtUtc.Should().Be(Now.AddDays(3));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_ForACarOfAnotherDealer()
    {
        using var context = CreateContext();
        var (car, client, _) = await SeedAsync(context, carDealer: OtherDealerId);

        var result = await Handler(context).Handle(Command(car.Id, client.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
        (await context.Deposits.CountAsync()).Should().Be(0);
    }

    // Security review: every referenced id must belong to the caller's dealer (and the quote to the same car), or a
    // deposit could point at another tenant's lead and the expiry sweep would email that lead.
    [Fact]
    public async Task Handle_Should_RejectALeadOrQuote_ThatIsNotTheCallers()
    {
        using var context = CreateContext();
        var (car, client, _) = await SeedAsync(context);
        var foreignLead = Domain.Leads.Lead.Create(OtherDealerId, "Eve", "eve@other.com", "222", Domain.Leads.LeadSource.Web, Now);
        var otherCar = new Car(DealerId, car.Marca, car.Modelo, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2021, "DEP002", "desc", 10000m, Now.AddDays(-30));
        var quoteOnOtherCar = new Quote(DealerId, otherCar, client, null, 10000m, QuotePaymentMethod.Contado, Now.AddDays(20), "q", Now.AddDays(-2));
        context.Leads.Add(foreignLead);
        context.Cars.Add(otherCar);
        context.Quotes.Add(quoteOnOtherCar);
        await context.SaveChangesAsync();

        var withForeignLead = await Handler(context).Handle(
            new CreateDepositCommand(car.Id, null, foreignLead.Id, null, 1000m, "ARS", PaymentMethod.Cash), CancellationToken.None);
        var withQuoteOfAnotherCar = await Handler(context).Handle(
            Command(car.Id, client.Id, quoteId: quoteOnOtherCar.Id), CancellationToken.None);

        withForeignLead.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
        withQuoteOfAnotherCar.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
        (await context.Deposits.CountAsync()).Should().Be(0);
    }
}
