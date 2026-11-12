using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Abstractions.Tenancy;
using Application.Quotes.Delete;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Quotes;

public class DeleteQuoteCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static ICurrentTenantService CreateTenantService(Guid? dealerId = null)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(dealerId ?? DealerId);
        return tenantMock.Object;
    }

    private static DeleteQuoteCommandHandler CreateHandler(TestApplicationDbContext context, Guid? dealerId = null) =>
        new DeleteQuoteCommandHandler(context, new FakeDateTimeProvider(), CreateTenantService(dealerId));

    private static async Task<(Quote Quote, Car Car)> SeedQuoteAsync(
        TestApplicationDbContext context,
        StatusServiceCar carStatus = StatusServiceCar.Reservado,
        Action<Quote>? mutateQuote = null)
    {
        var dealerId = DealerId;
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, carStatus, 4, 5, 1600, 1000, 2021, "DEL001", "desc", 15000m, DateTime.UtcNow);
        var client = new Client(dealerId, "Eve", "Black", "999", "eve@test.com", "444", "Addr", DateTime.UtcNow);
        var quote = new Quote(dealerId, car, client, null, 14000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(7), "c", DateTime.UtcNow);
        mutateQuote?.Invoke(quote);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();
        return (quote, car);
    }

    [Fact]
    public async Task Handle_Should_SoftDelete_SettingIsDeleted_WithoutRemovingRow()
    {
        using var context = CreateContext();
        var (quote, _) = await SeedQuoteAsync(context);
        var handler = CreateHandler(context);

        var result = await handler.Handle(new DeleteQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Row must still exist (soft delete, not physical removal).
        var persisted = await context.Quotes.IgnoreQueryFilters().FirstAsync(q => q.Id == quote.Id);
        persisted.IsDeleted.Should().BeTrue();
        persisted.DeletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenQuoteDoesNotExist()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);
        var missingId = Guid.NewGuid();

        var result = await handler.Handle(new DeleteQuoteCommand(missingId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(QuoteErrors.NotFound(missingId));
    }

    // ── Who actually holds the reservation ────────────────────────────────────────────────
    //
    // A Pending quote is an offer and holds nothing. The reservation belongs to the ACCEPTED
    // quote. Releasing the car when a Pending quote goes away was harmless while only one quote
    // per car could exist; now that several can, it would free a unit another buyer already
    // committed to — the loser's withdrawal cancelling the winner's hold.

    [Fact]
    public async Task Handle_Should_ReleaseCar_WhenDeletingAnAcceptedQuote()
    {
        using var context = CreateContext();
        var (quote, car) = await SeedQuoteAsync(
            context,
            StatusServiceCar.Reservado,
            q => q.Accept(DateTime.UtcNow));
        var handler = CreateHandler(context);

        var result = await handler.Handle(new DeleteQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persistedCar = await context.Cars.SingleAsync(c => c.Id == car.Id);
        persistedCar.ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }

    [Fact]
    public async Task Handle_Should_NotReleaseCar_WhenDeletingAPendingQuote()
    {
        // The car is Reservado because SOMEONE ELSE's quote was accepted. Deleting this losing
        // offer must not hand the unit back to the floor.
        using var context = CreateContext();
        var (quote, car) = await SeedQuoteAsync(context, StatusServiceCar.Reservado);
        var handler = CreateHandler(context);

        var result = await handler.Handle(new DeleteQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persistedCar = await context.Cars.SingleAsync(c => c.Id == car.Id);
        persistedCar.ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }

    [Fact]
    public async Task Handle_Should_NotReleaseCar_WhenQuoteWasRejected()
    {
        using var context = CreateContext();
        var (quote, car) = await SeedQuoteAsync(
            context,
            StatusServiceCar.Vendido,
            q => q.Reject("not interested anymore", DateTime.UtcNow));
        var handler = CreateHandler(context);

        var result = await handler.Handle(new DeleteQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persistedCar = await context.Cars.SingleAsync(c => c.Id == car.Id);
        persistedCar.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }
}
