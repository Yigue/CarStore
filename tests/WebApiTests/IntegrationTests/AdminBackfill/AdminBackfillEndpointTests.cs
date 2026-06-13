using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Cars.Commands.BackfillPrePhase2Images;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiTests.IntegrationTests.AdminBackfill;

/// <summary>
/// RED tests for <c>POST /api/v1/admin/backfill/pre-phase2-images</c> (REQ-FVIP-1).
/// Spec scenarios: dry-run, apply, validation rejection, anonymous 401.
///
/// The 403 "non-admin caller" scenario (proposal §1 Scenario 4) is deferred to a
/// follow-up commit because <see cref="WebApiTests.CustomWebApplicationFactory"/>
/// seeds only an admin user. Seeding a non-admin user is out of scope for Slice 1;
/// the authorization contract is exercised by the existing [HasPermission] gate
/// (see <see cref="Domain.Cars.Web.Api.Endpoints.Cars.Permissions.AdminBackfill"/>).
/// </summary>
public class AdminBackfillEndpointTests
{
    private sealed record BackfillResponse(
        [property: JsonPropertyName("auditId")] Guid AuditId,
        [property: JsonPropertyName("action")] string Action,
        [property: JsonPropertyName("affectedRowCount")] int AffectedRowCount,
        [property: JsonPropertyName("affectedImageIds")] IReadOnlyList<Guid> AffectedImageIds);

    [Fact]
    public async Task Post_AsAdmin_WithDryRun_Returns200_AndPreviewRows_LeavesImagesUnchanged()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        await SeedCarWithLegacyImage(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/backfill/pre-phase2-images",
            new { dryRun = true, confirmed = false },
            IntegrationTestHelpers.JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BackfillResponse>(IntegrationTestHelpers.JsonOptions);
        body.Should().NotBeNull();
        body!.Action.Should().Be("DryRun");
        body.AffectedRowCount.Should().BeGreaterThanOrEqualTo(1);

        // The legacy car_image row MUST still be IsCover=false after the dry-run.
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Infrastructure.Database.ApplicationDbContext>();
        var stillLegacy = await context.CarImages.IgnoreQueryFilters().FirstAsync();
        stillLegacy.IsCover.Should().BeFalse();
    }

    [Fact]
    public async Task Post_AsAdmin_WithApplyConfirmed_Returns200_AndFlipsCover()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        await SeedCarWithLegacyImage(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/backfill/pre-phase2-images",
            new { dryRun = false, confirmed = true },
            IntegrationTestHelpers.JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BackfillResponse>(IntegrationTestHelpers.JsonOptions);
        body.Should().NotBeNull();
        body!.Action.Should().Be("Apply");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Infrastructure.Database.ApplicationDbContext>();
        var image = await context.CarImages.IgnoreQueryFilters().FirstAsync();
        image.IsCover.Should().BeTrue();
    }

    [Fact]
    public async Task Post_AsAdmin_WithApplyNotConfirmed_Returns400()
    {
        // Arrange — apply without confirmation is the ambiguous "I want to apply but haven't said yes" case.
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/backfill/pre-phase2-images",
            new { dryRun = false, confirmed = false },
            IntegrationTestHelpers.JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_AsAnonymous_Returns401()
    {
        // Arrange — no Authorization header at all.
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/backfill/pre-phase2-images",
            new { dryRun = true, confirmed = false },
            IntegrationTestHelpers.JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Seeds a car with one CarImage that has <c>IsCover=false</c> — the legacy shape
    /// the backfill is designed to repair. Uses the dealer's admin user id so the
    /// global query filter does not strip the row.
    /// </summary>
    private static async Task SeedCarWithLegacyImage(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Infrastructure.Database.ApplicationDbContext>();
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);

        var marca = new Domain.Cars.Attributes.Marca("Toyota");
        var modelo = new Domain.Cars.Attributes.Modelo("Corolla", marca.Id);
        var car = new Domain.Cars.Car(
            dealerId,
            marca,
            modelo,
            Color.Black,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
            4, 5, 2000, 0, 2024,
            "BCK001",
            "Legacy car for backfill test",
            15000m,
            new DateTime(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc));

        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var legacyImage = new CarImage(
            car.Id,
            imageUrl: "https://legacy.example.com/old.jpg",
            isCover: false,
            displayOrder: 0);
        context.CarImages.Add(legacyImage);
        await context.SaveChangesAsync();
    }
}
