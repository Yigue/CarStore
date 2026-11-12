using Application.Public.TestDrive;
using Domain.Appointments;
using Domain.Cars;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests.Public;

/// <summary>
/// sfc B5.2.1 / RN-044: slots = <c>DealerSettings.BusinessHours</c> for the requested day, sliced
/// into fixed-length windows, minus the vehicle's existing conflicts. No hours configured for that
/// dealer/day → empty list, never a 500 and never a guessed default schedule.
/// </summary>
public class TestDriveSlotsQueryHandlerTests
{
    // 2024-01-01 is a Monday.
    private static readonly DateOnly Monday = new(2024, 1, 1);
    private static readonly DateOnly Tuesday = new(2024, 1, 2);

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context, Guid dealerId, string patente)
    {
        var marca = new Marca($"Peugeot-{patente}");
        var modelo = new Modelo($"208-{patente}", marca.Id);
        var car = new Car(
            dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, patente, "desc", 15000m, DateTime.UtcNow);

        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static async Task SeedBusinessHoursAsync(
        TestApplicationDbContext context,
        Guid dealerId,
        DayOfWeek day,
        TimeOnly open,
        TimeOnly close)
    {
        var settings = new DealerSettingsEntity(dealerId, "Test Dealer", "test@dealer.com");
        settings.SetBusinessHours([new Domain.DealerSettings.BusinessHoursEntry(day, open, close)]);
        context.DealerSettings.Add(settings);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_Returns404_WhenCarDoesNotExist()
    {
        using var context = CreateContext();
        var handler = new TestDriveSlotsQueryHandler(context);

        var result = await handler.Handle(new TestDriveSlotsQuery(Guid.NewGuid(), Monday), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TestDrive.CarNotFound");
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenDealerHasNoBusinessHoursConfigured()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDA001");
        // No DealerSettings row at all.

        var handler = new TestDriveSlotsQueryHandler(context);
        var result = await handler.Handle(new TestDriveSlotsQuery(car.Id, Monday), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenTheRequestedDayHasNoHours()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDA002");
        await SeedBusinessHoursAsync(context, dealerId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0));

        var handler = new TestDriveSlotsQueryHandler(context);
        var result = await handler.Handle(new TestDriveSlotsQuery(car.Id, Tuesday), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsFixedLengthSlots_WithinTheConfiguredWindow()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDA003");
        await SeedBusinessHoursAsync(context, dealerId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0));

        var handler = new TestDriveSlotsQueryHandler(context);
        var result = await handler.Handle(new TestDriveSlotsQuery(car.Id, Monday), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4, "9:00-11:00 sliced into 30-minute slots is 4 slots");
        result.Value.Select(s => s.Start.TimeOfDay).Should().BeEquivalentTo(new[]
        {
            new TimeSpan(9, 0, 0),
            new TimeSpan(9, 30, 0),
            new TimeSpan(10, 0, 0),
            new TimeSpan(10, 30, 0),
        });
        result.Value.First().End.TimeOfDay.Should().Be(new TimeSpan(9, 30, 0));
    }

    [Fact]
    public async Task Handle_ExcludesSlots_WithAnExistingVehicleConflict()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDA004");
        await SeedBusinessHoursAsync(context, dealerId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0));

        // A Requested appointment (no agent) on the same vehicle blocks exactly its own window.
        DateTime busyStart = Monday.ToDateTime(new TimeOnly(10, 0));
        DateTime busyEnd = Monday.ToDateTime(new TimeOnly(10, 30));
        var busy = Appointment.Request(
            dealerId, car.Id, clientId: null, leadId: Guid.NewGuid(),
            busyStart, busyEnd, AppointmentType.TestDrive, null, new DateTime(2024, 1, 1));
        busy.ClearDomainEvents();
        context.Appointments.Add(busy);
        await context.SaveChangesAsync();

        var handler = new TestDriveSlotsQueryHandler(context);
        var result = await handler.Handle(new TestDriveSlotsQuery(car.Id, Monday), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Select(s => s.Start.TimeOfDay).Should().NotContain(new TimeSpan(10, 0, 0));
    }
}
