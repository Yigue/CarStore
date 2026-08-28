using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Storage;
using Application.Cars.Delete;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Leads;
using Domain.Quotes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Cars;

/// <summary>
/// Deleting a vehicle used to be unconditionally physical. Five foreign keys reference Car with
/// <c>DeleteBehavior.Restrict</c> — Appointment, FinancialTransaction, Lead, Quote and Sale —
/// so any vehicle someone had merely enquired about (the public inquiry endpoint creates a
/// Quote) hit a database constraint. The handler had already deleted every MinIO blob by then,
/// so the operator got a 500, the vehicle stayed, and its photos were gone.
///
/// The handler now counts those references first: clean vehicles still take the physical path
/// REQ-VMS-5 / ADR-5 specifies, referenced ones are withdrawn instead.
/// </summary>
public class DeleteCarCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    /// <summary>
    /// Images are attached before the first save, the way DeleteCarCascadeTests seeds them: the
    /// InMemory provider cannot insert a child added to an already-persisted parent collection.
    /// </summary>
    private static async Task<Car> SeedCarAsync(
        TestApplicationDbContext context,
        string patente,
        string? objectKey = null)
    {
        var marca = new Marca("Fiat");
        var modelo = new Modelo("Cronos", marca.Id);
        var car = new Car(
            DealerId, marca, modelo, Color.Gray, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1300, 30000, 2021, patente, "del test",
            9000m, DateTime.UtcNow);

        if (objectKey is not null)
        {
            car.Images.Add(CarImage.Create(Guid.NewGuid(), car.Id, objectKey, "image/jpeg", 100, 0, true));
        }

        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static DeleteCarCommandHandler CreateHandler(
        TestApplicationDbContext context,
        Mock<IStorageService>? storage = null) =>
        new(context,
            (storage ?? new Mock<IStorageService>()).Object,
            NullLogger<DeleteCarCommandHandler>.Instance,
            new FakeDateTimeProvider());

    [Fact]
    public async Task Handle_Should_WithdrawTheVehicle_WhenAQuoteReferencesIt()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "QUO001");

        var client = new Client(DealerId, "Ana", "Diaz", "1", "ana@test.com", "2", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        context.Quotes.Add(new Quote(
            DealerId, car, client, null, 9000m, Domain.Quotes.Attributes.PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "interesado", DateTime.UtcNow));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new DeleteCarCommand(car.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Car? stored = await context.Cars.SingleOrDefaultAsync(c => c.Id == car.Id);
        stored.Should().NotBeNull("the quote still points at this row");
        stored!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_WithdrawTheVehicle_WhenALeadIsInterestedInIt()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "LEA001");

        context.Leads.Add(Lead.Create(
            DealerId, "Juan Perez", "juan@test.com", "555", LeadSource.Web, DateTime.UtcNow, car.Id));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new DeleteCarCommand(car.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Cars.SingleAsync(c => c.Id == car.Id)).IsDeleted.Should().BeTrue();
    }

    /// <summary>
    /// A withdrawn vehicle keeps its photos: the lead or quote that blocked deletion still
    /// renders the unit, and the operator can restore it. Deleting the blobs here would leave
    /// those references pointing at an image-less record with no way back.
    /// </summary>
    [Fact]
    public async Task Handle_Should_KeepBlobs_WhenTheVehicleIsOnlyWithdrawn()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "BLO001", "cars/x/y.jpg");

        context.Leads.Add(Lead.Create(
            DealerId, "Juan Perez", "juan@test.com", "555", LeadSource.Web, DateTime.UtcNow, car.Id));
        await context.SaveChangesAsync();

        var storage = new Mock<IStorageService>();

        var result = await CreateHandler(context, storage).Handle(new DeleteCarCommand(car.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        storage.Verify(
            s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// The clean case is unchanged: REQ-VMS-5 / ADR-5 still applies when nothing references the
    /// vehicle. This test exists so the new branch cannot quietly swallow the physical path.
    /// </summary>
    [Fact]
    public async Task Handle_Should_PhysicallyDeleteTheVehicle_WhenNothingReferencesIt()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "CLN001", "cars/x/z.jpg");

        var storage = new Mock<IStorageService>();

        var result = await CreateHandler(context, storage).Handle(new DeleteCarCommand(car.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Cars.AnyAsync(c => c.Id == car.Id)).Should().BeFalse();
        storage.Verify(
            s => s.DeleteFileAsync("cars/x/z.jpg", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A second DELETE on an already-withdrawn vehicle returns 404, not success: the handler
    // reads through the global filter, which hides withdrawn rows. That behaviour cannot be
    // asserted here — TestApplicationDbContext applies the entity configurations but not the
    // global filters — so it is covered by CarSoftDeleteFilterTests against the real
    // ApplicationDbContext instead of by a test that would pass for the wrong reason.
}
