using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Quotes.CreateInquiry;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Quotes;

/// <summary>
/// The public inquiry endpoint is <c>AllowAnonymous</c>, so no tenant is resolved and the
/// global query filter on <see cref="Client"/> is disabled for the whole request
/// (see <c>ApplicationDbContext.OnModelCreating</c>: <c>!_tenantService.HasTenant || ...</c>).
/// Every lookup inside this handler must therefore scope by DealerId explicitly.
/// TestApplicationDbContext mirrors that state: it applies the entity configurations but
/// not the global filters, so an unscoped query here sees every dealer's rows.
/// </summary>
public class CreateInquiryCommandHandlerTests
{
    private const string SharedEmail = "shared@buyer.com";

    private static readonly Guid DealerA = Guid.NewGuid();
    private static readonly Guid DealerB = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context, Guid dealerId, string patente)
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(
            dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, patente, "desc", 15000m, DateTime.UtcNow);

        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static async Task<Client> SeedClientAsync(TestApplicationDbContext context, Guid dealerId, string email)
    {
        var client = new Client(dealerId, "Existing", "Buyer", "111", email, "222", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return client;
    }

    private static CreateInquiryCommand BuildCommand(Guid? carId, string email) => new(
        carId,
        "Ana",
        "Fernandez",
        email,
        "1122334455",
        "Me interesa este vehiculo");

    /// <summary>
    /// Two dealers each hold a client with the same email — a normal situation, since a buyer
    /// may shop at several dealerships. An unscoped <c>SingleOrDefaultAsync</c> on email alone
    /// matches both rows and throws, turning a public inquiry into a 500.
    /// </summary>
    [Fact]
    public async Task Handle_Should_Succeed_WhenAnotherDealerHasAClientWithTheSameEmail()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "AAA111");
        await SeedClientAsync(context, DealerA, SharedEmail);
        await SeedClientAsync(context, DealerB, SharedEmail);

        var handler = new CreateInquiryCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(BuildCommand(car.Id, SharedEmail), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    /// <summary>
    /// The inquiry must never attach the requesting dealer's quote to a client owned by another
    /// tenant. Only DealerB holds this email, so DealerA's inquiry has to create its own client.
    /// </summary>
    [Fact]
    public async Task Handle_Should_CreateOwnClient_WhenOnlyAnotherDealerHasThatEmail()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "AAA222");
        Client foreignClient = await SeedClientAsync(context, DealerB, SharedEmail);

        var handler = new CreateInquiryCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(BuildCommand(car.Id, SharedEmail), CancellationToken.None);

        Assert.True(result.IsSuccess);

        Quote quote = await context.Quotes.SingleAsync(q => q.CarId == car.Id);
        Assert.NotNull(quote.ClientId);
        Assert.NotEqual(foreignClient.Id, quote.ClientId);

        Client attached = await context.Clients.SingleAsync(c => c.Id == quote.ClientId);
        Assert.Equal(DealerA, attached.DealerId);
    }

    /// <summary>
    /// Within a single dealer the handler still reuses the existing client instead of
    /// duplicating it — the scoping fix must not turn every repeat inquiry into a new record.
    /// </summary>
    [Fact]
    public async Task Handle_Should_ReuseExistingClient_WhenSameDealerAlreadyHasThatEmail()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "AAA333");
        Client existing = await SeedClientAsync(context, DealerA, SharedEmail);

        var handler = new CreateInquiryCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(BuildCommand(car.Id, SharedEmail), CancellationToken.None);

        Assert.True(result.IsSuccess);

        Quote quote = await context.Quotes.SingleAsync(q => q.CarId == car.Id);
        Assert.Equal(existing.Id, quote.ClientId);
        Assert.Single(context.Clients.Where(c => c.DealerId == DealerA));
    }
}
