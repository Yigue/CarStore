using Application.Public.TestDrive;
using Domain.Appointments;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests.Public;

/// <summary>
/// sfc B5.2.2/B5.2.4 / RN-044.
///
/// CAPTCHA enforcement ("sin captcha válido → 400") is NOT exercised here: it happens in
/// <c>Web.Api.Infrastructure.CaptchaEndpointFilter</c>, BEFORE the handler ever runs — same
/// architecture as <c>POST dealers/provision</c>, whose equivalent HTTP-level captcha contract
/// lives in <c>WebApiTests/Dealers/ProvisionEndpointTests.cs</c>, not in an ApplicationTests
/// handler test (see <c>ApplicationTests/Dealers/ProvisionCaptchaTests.cs</c>'s doc comment for
/// the same reasoning). The equivalent coverage for this endpoint is
/// <c>WebApiTests/Public/TestDriveEndpointTests.cs</c>. What IS a handler-level concern — and is
/// covered here — is the honeypot and the RN-044 "max 2 simultaneous Requested" throttle.
/// </summary>
public class CreateTestDriveCommandHandlerTests
{
    private static readonly DateTime Now = new(2024, 1, 1, 8, 0, 0); // Monday
    private static readonly DateTime SlotStart = new(2024, 1, 1, 9, 0, 0);

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

    private static async Task SeedBusinessHoursAsync(TestApplicationDbContext context, Guid dealerId)
    {
        var settings = new DealerSettingsEntity(dealerId, "Test Dealer", "test@dealer.com");
        settings.SetBusinessHours([new Domain.DealerSettings.BusinessHoursEntry(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0))]);
        context.DealerSettings.Add(settings);
        await context.SaveChangesAsync();
    }

    private static CreateTestDriveCommand BuildCommand(
        Guid carId,
        string email = "visitor@example.com",
        string phone = "1122334455",
        DateTime? start = null,
        string? website = null) =>
        new(carId, "Ana", "Fernandez", email, phone, start ?? SlotStart, "Quiero probarlo", website);

    [Fact]
    public async Task Handle_Creates_RequestedAppointment_NotScheduled()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDB001");
        await SeedBusinessHoursAsync(context, dealerId);
        var handler = new CreateTestDriveCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now });

        var result = await handler.Handle(BuildCommand(car.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Appointment appointment = await context.Appointments.SingleAsync();
        appointment.Status.Should().Be(AppointmentStatus.Requested);
        appointment.AgentId.Should().BeNull();
        appointment.VehicleId.Should().Be(car.Id);
    }

    [Fact]
    public async Task Handle_CreatesALead_AndAdvancesItToDemostracion()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDB002");
        await SeedBusinessHoursAsync(context, dealerId);
        var handler = new CreateTestDriveCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now });

        await handler.Handle(BuildCommand(car.Id), CancellationToken.None);

        Lead lead = await context.Leads.SingleAsync();
        lead.Status.Should().Be(LeadStatus.Demostracion);
        lead.InterestedVehicleId.Should().Be(car.Id);
    }

    [Fact]
    public async Task Handle_WithHoneypotFilled_ReturnsSuccess_ButCreatesNothing()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDB003");
        await SeedBusinessHoursAsync(context, dealerId);
        var handler = new CreateTestDriveCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now });

        var result = await handler.Handle(BuildCommand(car.Id, website: "https://spam.example"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a bot must not learn it was detected");
        context.Appointments.Should().BeEmpty();
        context.Leads.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Fails_WhenOutsideBusinessHours()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDB004");
        await SeedBusinessHoursAsync(context, dealerId); // Monday 9-11

        var handler = new CreateTestDriveCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now });
        var outsideHours = new DateTime(2024, 1, 1, 20, 0, 0); // same Monday, 20:00 — after close

        var result = await handler.Handle(BuildCommand(car.Id, start: outsideHours), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TestDrive.OutsideBusinessHours");
        context.Appointments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Fails_WhenTheVehicleAlreadyHasAConflict()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDB005");
        await SeedBusinessHoursAsync(context, dealerId);

        var existing = Appointment.Request(
            dealerId, car.Id, clientId: null, leadId: Guid.NewGuid(),
            SlotStart, SlotStart.AddMinutes(30), AppointmentType.TestDrive, null, Now);
        existing.ClearDomainEvents();
        context.Appointments.Add(existing);
        await context.SaveChangesAsync();

        var handler = new CreateTestDriveCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now });
        var result = await handler.Handle(BuildCommand(car.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TestDrive.Conflict");
    }

    [Fact]
    public async Task Handle_Rejects_TheThirdSimultaneousRequestedBooking_ForTheSameEmail()
    {
        using var context = CreateContext();
        Guid dealerId = Guid.NewGuid();
        Car car = await SeedCarAsync(context, dealerId, "TDB006");
        await SeedBusinessHoursAsync(context, dealerId);
        var clock = new FakeDateTimeProvider { UtcNow = Now };
        var handler = new CreateTestDriveCommandHandler(context, clock);
        const string email = "repeat@example.com";

        // First and second requests for different cars (so they don't conflict on vehicle/time),
        // same contact — both must succeed and stay Requested.
        Car car2 = await SeedCarAsync(context, dealerId, "TDC001");
        Car car3 = await SeedCarAsync(context, dealerId, "TDC002");

        var first = await handler.Handle(BuildCommand(car.Id, email: email), CancellationToken.None);
        var second = await handler.Handle(BuildCommand(car2.Id, email: email), CancellationToken.None);
        var third = await handler.Handle(BuildCommand(car3.Id, email: email), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        third.IsFailure.Should().BeTrue();
        third.Error.Code.Should().Be("TestDrive.TooManyRequested");
        context.Appointments.Count().Should().Be(2, "the third booking must not be persisted");
    }

    [Fact]
    public async Task Handle_Fails_WhenCarDoesNotExist()
    {
        using var context = CreateContext();
        var handler = new CreateTestDriveCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now });

        var result = await handler.Handle(BuildCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TestDrive.CarNotFound");
    }
}
