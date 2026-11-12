using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Security;
using Domain.Cars;
using Domain.Cars.Attributes;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebApiTests.Fakes;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace WebApiTests.Public;

/// <summary>
/// sfc B5.2.2/B5.2.4 / RN-044 — HTTP-level contract for <c>POST public/test-drives</c> and
/// <c>GET public/cars/{id}/test-drive-slots</c>.
///
/// The CAPTCHA 400/503 contract below is the deviation documented in
/// <c>ApplicationTests/Public/CreateTestDriveCommandHandlerTests.cs</c>'s doc comment: it belongs
/// here (HTTP/endpoint-filter level), not in an ApplicationTests handler test, matching the
/// existing <c>ProvisionEndpointTests.cs</c> precedent for the same
/// <see cref="Web.Api.Infrastructure.CaptchaEndpointFilter"/>.
/// </summary>
public class TestDriveEndpointTests
{
    private const string ValidCaptchaToken = "test-captcha-token";

    /// <summary>
    /// <see cref="CustomWebApplicationFactory"/>'s TestServer defaults the Host header to
    /// "localhost", and <c>DealerSettingsSeeder</c> seeds exactly one dealer with that HostName
    /// (<see cref="CustomWebApplicationFactory.AdminDealerId"/>) so anonymous requests resolve a
    /// tenant — see that seeder's doc comment. A car seeded under any OTHER dealer id is
    /// query-filtered out of every anonymous request in these tests, so this helper reuses that
    /// exact seeded dealer rather than creating a second, invisible one.
    /// </summary>
    private static async Task<(Guid DealerId, Guid CarId)> SeedDealerWithCarAndMondayHoursAsync(
        CustomWebApplicationFactory factory,
        string patente)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        Guid dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        DealerSettingsEntity settings = await db.DealerSettings
            .IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == dealerId);
        settings.SetBusinessHours([new Domain.DealerSettings.BusinessHoursEntry(
            DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0))]);

        var marca = new Marca($"Marca-{patente}");
        var modelo = new Modelo($"Modelo-{patente}", marca.Id);
        db.Marca.Add(marca);
        db.Modelo.Add(modelo);
        await db.SaveChangesAsync();

        var car = new Car(
            dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, patente, "desc", 15000m, DateTime.UtcNow);
        db.Cars.Add(car);
        await db.SaveChangesAsync();

        return (dealerId, car.Id);
    }

    private static object ValidBody(Guid carId, DateTime start, string email = "visitor@example.com") => new
    {
        CarId = carId,
        FirstName = "Ana",
        LastName = "Fernandez",
        Email = email,
        Phone = "1122334455",
        Start = start,
        Comments = "Quiero probarlo",
        CaptchaToken = ValidCaptchaToken
    };

    /// <summary>
    /// <c>Appointment.Request</c> rejects a past date against the REAL <c>IDateTimeProvider</c>
    /// clock (this is an integration test — no <c>FakeDateTimeProvider</c> here), so the slot must
    /// always be a Monday strictly after "today", not a fixed historical date.
    /// </summary>
    private static readonly DateTime MondaySlotStart = NextMonday9Am();

    private static DateTime NextMonday9Am()
    {
        DateTime today = DateTime.UtcNow.Date;
        int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        daysUntilMonday = daysUntilMonday == 0 ? 7 : daysUntilMonday; // always a future Monday, never today
        return today.AddDays(daysUntilMonday).AddHours(9);
    }

    [Fact]
    public async Task CreateTestDrive_ReturnsCreated_WithValidCaptchaWithinBusinessHours()
    {
        await using var factory = new CustomWebApplicationFactory();
        (Guid dealerId, Guid carId) = await SeedDealerWithCarAndMondayHoursAsync(factory, "TDE001");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/public/test-drives", ValidBody(carId, MondaySlotStart));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTestDrive_ReturnsBadRequest_WhenCaptchaTokenMissing_AndCreatesNoAppointment()
    {
        await using var factory = new CustomWebApplicationFactory();
        (Guid dealerId, Guid carId) = await SeedDealerWithCarAndMondayHoursAsync(factory, "TDE002");
        var client = factory.CreateClient();

        var body = new
        {
            CarId = carId,
            FirstName = "Ana",
            LastName = "Fernandez",
            Email = "nocaptcha@example.com",
            Phone = "1122334455",
            Start = MondaySlotStart,
            Comments = "Quiero probarlo"
            // CaptchaToken intentionally omitted.
        };

        var response = await client.PostAsJsonAsync("/api/v1/public/test-drives", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoAppointmentPersistedAsync(factory, carId);
    }

    [Fact]
    public async Task CreateTestDrive_ReturnsBadRequest_WhenCaptchaInvalid_AndCreatesNoAppointment()
    {
        await using var factory = new CaptchaStubWebApplicationFactory(CaptchaVerificationResult.Invalid);
        (Guid dealerId, Guid carId) = await SeedDealerWithCarAndMondayHoursAsync(factory, "TDE003");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/public/test-drives", ValidBody(carId, MondaySlotStart, "badcaptcha@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoAppointmentPersistedAsync(factory, carId);
    }

    [Fact]
    public async Task CreateTestDrive_ReturnsServiceUnavailable_WhenCaptchaProviderUnavailable()
    {
        await using var factory = new CaptchaStubWebApplicationFactory(CaptchaVerificationResult.Unavailable);
        (Guid dealerId, Guid carId) = await SeedDealerWithCarAndMondayHoursAsync(factory, "TDE004");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/public/test-drives", ValidBody(carId, MondaySlotStart, "downcaptcha@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await AssertNoAppointmentPersistedAsync(factory, carId);
    }

    [Fact]
    public async Task CreateTestDrive_WithHoneypotFilled_ReturnsCreated_ButCreatesNoAppointment()
    {
        await using var factory = new CustomWebApplicationFactory();
        (Guid dealerId, Guid carId) = await SeedDealerWithCarAndMondayHoursAsync(factory, "TDE005");
        var client = factory.CreateClient();

        var body = new
        {
            CarId = carId,
            FirstName = "Bot",
            LastName = "Spam",
            Email = "bot@example.com",
            Phone = "0000000000",
            Start = MondaySlotStart,
            Comments = (string?)null,
            Website = "https://spam.example",
            CaptchaToken = ValidCaptchaToken
        };

        var response = await client.PostAsJsonAsync("/api/v1/public/test-drives", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created, "a bot must see the same response shape a human would");
        await AssertNoAppointmentPersistedAsync(factory, carId);
    }

    [Fact]
    public async Task GetTestDriveSlots_ReturnsOk_WithSlotsInsideConfiguredBusinessHours()
    {
        await using var factory = new CustomWebApplicationFactory();
        (Guid dealerId, Guid carId) = await SeedDealerWithCarAndMondayHoursAsync(factory, "TDE006");
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/public/cars/{carId}/test-drive-slots?date=2024-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var slots = await response.Content.ReadFromJsonAsync<List<TestDriveSlotDto>>(IntegrationTestHelpers.JsonOptions);
        slots.Should().NotBeNull();
        slots!.Should().HaveCount(4, "9:00-11:00 sliced into 30-minute slots is 4 slots");
    }

    private sealed record TestDriveSlotDto(DateTime Start, DateTime End);

    private static async Task AssertNoAppointmentPersistedAsync(CustomWebApplicationFactory factory, Guid carId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Appointments.IgnoreQueryFilters().AnyAsync(a => a.VehicleId == carId))
            .Should().BeFalse($"no appointment should exist for vehicle '{carId}' after a rejected/honeypot request");
    }

    /// <summary>Boots the normal Testing host but swaps <see cref="ICaptchaVerifier"/> for a deterministic stub.</summary>
    private sealed class CaptchaStubWebApplicationFactory(CaptchaVerificationResult result) : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICaptchaVerifier>();
                services.AddSingleton<ICaptchaVerifier>(new StubCaptchaVerifier { Result = result });
            });
        }
    }
}
