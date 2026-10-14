using Application.Abstractions.Email;
using Application.Deposits.Expire;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Deposits;
using Domain.Deposits.Events;
using Domain.Quotes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuotePaymentMethod = Domain.Quotes.Attributes.PaymentMethod;

namespace Application.UnitTests.Deposits;

/// <summary>
/// sfc B1.3.3 / RN-002 / RN-003 / H6: the hourly sweep expires due deposits across every dealer and releases the car
/// only when nothing else still holds it (an accepted quote or a pending sale). 24 h before expiry the buyer gets one
/// DepositExpiring email, never a duplicate.
/// </summary>
public class ExpireDepositsJobTests
{
    private static readonly Guid DealerA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DealerB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITransactionalEmailSender> _email = new();

    // No dealer on the context: the sweep runs platform-wide from Quartz with no tenant.
    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private ExpireDueDepositsCommandHandler Handler(TestApplicationDbContext context) =>
        new(context, new FakeDateTimeProvider { UtcNow = Now }, _email.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ExpireDueDepositsCommandHandler>.Instance);

    private static async Task<(Car Car, Client Client)> SeedReservedCarAsync(TestApplicationDbContext context, Guid dealerId, string plate)
    {
        context.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(dealerId, "Autos " + plate, $"{plate}@dealer.com"));
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2021, plate, "desc", 10000m, Now.AddDays(-30));
        car.Reserve(Now.AddDays(-8));
        var client = new Client(dealerId, "Ana", "Perez", "30111222", $"{plate.ToLowerInvariant()}@buyer.com", "111", "Addr", Now.AddDays(-30));
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return (car, client);
    }

    private static Deposit NewDeposit(Guid dealerId, Car car, Client client, DateTime createdAt, int validityDays = 7) =>
        Deposit.Create(dealerId, car.Id, client.Id, null, 1000m, "ARS", null, createdAt,
            new DepositCarContext(StatusServiceCar.Disponible, null, false, "ARS"), validityDays).Value;

    [Fact]
    public async Task Handle_Should_ExpireDueDeposits_OfEveryDealer_AndReleaseTheirCars()
    {
        using var context = CreateContext();
        var (carA, clientA) = await SeedReservedCarAsync(context, DealerA, "AAA111");
        var (carB, clientB) = await SeedReservedCarAsync(context, DealerB, "BBB222");
        context.Deposits.AddRange(NewDeposit(DealerA, carA, clientA, Now.AddDays(-8)), NewDeposit(DealerB, carB, clientB, Now.AddDays(-8)));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(new ExpireDueDepositsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Expired.Should().Be(2);
        result.Value.CarsReleased.Should().Be(2);
        (await context.Deposits.Select(d => d.Status).ToListAsync()).Should().OnlyContain(s => s == DepositStatus.Expired);
        (await context.Cars.Select(c => c.ServiceCar).ToListAsync()).Should().OnlyContain(s => s == StatusServiceCar.Disponible);
    }

    [Fact]
    public async Task Handle_Should_KeepTheCarReserved_WhenAnAcceptedQuoteStillHoldsIt()
    {
        using var context = CreateContext();
        var (car, client) = await SeedReservedCarAsync(context, DealerA, "HLD001");
        var quote = new Quote(DealerA, car, client, null, 10000m, QuotePaymentMethod.Contado, Now.AddDays(20), "q", Now.AddDays(-9));
        quote.Accept(Now.AddDays(-9));
        context.Quotes.Add(quote);
        var deposit = NewDeposit(DealerA, car, client, Now.AddDays(-8));
        context.Deposits.Add(deposit);
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(new ExpireDueDepositsCommand(), CancellationToken.None);

        result.Value.Expired.Should().Be(1);
        result.Value.CarsReleased.Should().Be(0);
        deposit.DomainEvents.Should().ContainSingle(e => e is DepositExpiredDomainEvent);
        (await context.Cars.SingleAsync()).ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }

    [Fact]
    public async Task Handle_Should_LeaveDepositsThatAreNotDueYet()
    {
        using var context = CreateContext();
        var (car, client) = await SeedReservedCarAsync(context, DealerA, "NOT001");
        context.Deposits.Add(NewDeposit(DealerA, car, client, Now.AddDays(-2)));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(new ExpireDueDepositsCommand(), CancellationToken.None);

        result.Value.Expired.Should().Be(0);
        (await context.Deposits.SingleAsync()).Status.Should().Be(DepositStatus.Active);
    }

    [Fact]
    public async Task Handle_Should_SendTheExpiringNotice_Once_WithinTheLast24Hours()
    {
        using var context = CreateContext();
        var (car, client) = await SeedReservedCarAsync(context, DealerA, "NTC001");
        // Created 6.5 days ago with 7-day validity: expires in 12 hours.
        context.Deposits.Add(NewDeposit(DealerA, car, client, Now.AddDays(-6.5)));
        await context.SaveChangesAsync();

        var first = await Handler(context).Handle(new ExpireDueDepositsCommand(), CancellationToken.None);
        var second = await Handler(context).Handle(new ExpireDueDepositsCommand(), CancellationToken.None);

        first.Value.NoticesSent.Should().Be(1);
        second.Value.NoticesSent.Should().Be(0);
        _email.Verify(e => e.SendAsync(
            EmailTemplate.DepositExpiring,
            "ntc001@buyer.com",
            It.Is<IReadOnlyDictionary<string, string>>(m => m["VehicleName"] == "Toyota Corolla" && m["Name"] == "Ana Perez"),
            It.IsAny<CancellationToken>()), Times.Once);
        (await context.Deposits.SingleAsync()).Status.Should().Be(DepositStatus.Active);
    }
}
