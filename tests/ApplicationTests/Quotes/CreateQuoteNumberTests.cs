using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Quotes.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Quotes;

/// <summary>sfc B4.3 / RN-018: `Quote.Number` formatting and per-dealer folio allocation on creation.</summary>
public class CreateQuoteNumberTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid OtherDealerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ICurrentTenantService TenantFor(Guid dealerId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.SetupGet(t => t.HasTenant).Returns(true);
        mock.SetupGet(t => t.DealerId).Returns(dealerId);
        return mock.Object;
    }

    private static CreateQuoteCommandHandler Handler(TestApplicationDbContext context, Guid dealerId) =>
        new(context, new FakeDateTimeProvider { UtcNow = Now }, TenantFor(dealerId), new Infrastructure.Common.DealerSequenceAllocator(context));

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context, Guid dealerId, string plate)
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2021, plate, "desc", 15000m, Now.AddDays(-10));
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static async Task<Client> SeedClientAsync(TestApplicationDbContext context, Guid dealerId)
    {
        var client = new Client(dealerId, "Eve", "Black", "999", $"eve{Guid.NewGuid()}@test.com", "444", "Addr", Now);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return client;
    }

    [Fact]
    public void FormatNumber_Should_PadYearAndSequenceAsSpecified()
    {
        Quote.FormatNumber(2026, 1).Should().Be("COT-2026-000001");
        Quote.FormatNumber(2026, 42).Should().Be("COT-2026-000042");
        Quote.FormatNumber(2026, 1_000_000).Should().Be("COT-2026-1000000");
    }

    [Fact]
    public void AssignNumber_Should_RejectASecondAssignment()
    {
        var car = new Car(DealerId, new Marca("Ford"), new Modelo("Ka", Guid.NewGuid()), Color.Blue, TypeCar.Sedan,
            StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1400, 900, 2020, "ABC123", "d", 10000m, Now.AddDays(-5));
        var client = new Client(DealerId, "A", "B", "1", "a@b.com", "1", "addr", Now.AddDays(-5));
        var quote = new Quote(DealerId, car, client, null, 1000m, PaymentMethod.Contado, Now.AddDays(10), "c", Now.AddDays(-1));

        quote.AssignNumber("COT-2026-000001");

        Action act = () => quote.AssignNumber("COT-2026-000002");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public async Task Handle_Should_AssignSequentialFoliosForTheSameDealer()
    {
        using var context = CreateContext();
        var car1 = await SeedCarAsync(context, DealerId, "FOL001");
        var car2 = await SeedCarAsync(context, DealerId, "FOL002");
        var client = await SeedClientAsync(context, DealerId);
        var handler = Handler(context, DealerId);

        var first = await handler.Handle(
            new CreateQuoteCommand(car1.Id, client.Id, null, 10000m, PaymentMethod.Contado, Now.AddDays(10), "c"),
            CancellationToken.None);
        var second = await handler.Handle(
            new CreateQuoteCommand(car2.Id, client.Id, null, 11000m, PaymentMethod.Contado, Now.AddDays(10), "c"),
            CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();

        Quote firstQuote = await context.Quotes.SingleAsync(q => q.Id == first.Value);
        Quote secondQuote = await context.Quotes.SingleAsync(q => q.Id == second.Value);

        firstQuote.Number.Should().Be("COT-2026-000001");
        secondQuote.Number.Should().Be("COT-2026-000002");
    }

    [Fact]
    public async Task Handle_Should_NumberEachDealerFromItsOwnSequence()
    {
        using var context = CreateContext();
        var carForDealer = await SeedCarAsync(context, DealerId, "FOL010");
        var carForOtherDealer = await SeedCarAsync(context, OtherDealerId, "FOL011");
        var clientForDealer = await SeedClientAsync(context, DealerId);
        var clientForOtherDealer = await SeedClientAsync(context, OtherDealerId);

        var result = await Handler(context, DealerId).Handle(
            new CreateQuoteCommand(carForDealer.Id, clientForDealer.Id, null, 10000m, PaymentMethod.Contado, Now.AddDays(10), "c"),
            CancellationToken.None);
        var otherResult = await Handler(context, OtherDealerId).Handle(
            new CreateQuoteCommand(carForOtherDealer.Id, clientForOtherDealer.Id, null, 10000m, PaymentMethod.Contado, Now.AddDays(10), "c"),
            CancellationToken.None);

        Quote quote = await context.Quotes.SingleAsync(q => q.Id == result.Value);
        Quote otherQuote = await context.Quotes.SingleAsync(q => q.Id == otherResult.Value);

        // Both are "000001" — each dealer's counter starts independently, proving the sequence is
        // scoped by (dealer_id, kind), not shared across tenants.
        quote.Number.Should().Be("COT-2026-000001");
        otherQuote.Number.Should().Be("COT-2026-000001");
    }
}
