using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.IntegrationTests.Cars;

/// <summary>REQ-VMS-5 / ADR-5 — Car delete cascades blob deletion atomically.</summary>
public class DeleteCarCascadeTests
{
    private static async Task<Guid> SeedCarWithImagesAsync(CustomWebApplicationFactory factory, int imageCount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var brand = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Toyota");
        var model = await context.Modelo.IgnoreQueryFilters()
            .FirstAsync(m => m.Nombre == "Corolla" && m.MarcaId == brand.Id);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var car = new Car(dealerId, brand, model, Color.Red, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1800, 1000, 2021,
            $"AAA{Random.Shared.Next(100, 999)}", "cascade test", 12000m, DateTime.UtcNow);

        for (int i = 0; i < imageCount; i++)
        {
            var imageId = Guid.NewGuid();
            string key = $"cars/{dealerId}/{car.Id}/{imageId}.jpg";
            var image = CarImage.Create(imageId, car.Id, key, "image/jpeg", 1024, i, isCover: i == 0);
            car.Images.Add(image);

            // Mirror the blob into the fake store so deletion can be observed.
            using var ms = new System.IO.MemoryStream(new byte[] { 1, 2, 3 });
            await factory.Storage.UploadFileAsync(ms, key, "image/jpeg", default);
        }

        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car.Id;
    }

    [Fact]
    public async Task DeleteCar_RemovesAllBlobsAndDbRows()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        Guid carId = await SeedCarWithImagesAsync(factory, 3);
        factory.Storage.Count.Should().Be(3);

        var response = await client.DeleteAsync($"/api/v1/cars/{carId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        factory.Storage.Count.Should().Be(0);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Cars.IgnoreQueryFilters().AnyAsync(c => c.Id == carId)).Should().BeFalse();
        (await context.CarImages.IgnoreQueryFilters().AnyAsync(i => i.CarId == carId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCar_BlobDeleteFails_RollsBackDbDelete()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        Guid carId = await SeedCarWithImagesAsync(factory, 2);

        // Fail the delete of the SECOND blob (deterministic by ordering: keys differ per image).
        var keysSeen = new System.Collections.Generic.List<string>();
        factory.Storage.FailDeleteWhen = key =>
        {
            keysSeen.Add(key);
            return keysSeen.Count == 2; // throw on the 2nd delete
        };

        var response = await client.DeleteAsync($"/api/v1/cars/{carId}");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // Car must remain (DB delete rolled back / never committed).
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Cars.IgnoreQueryFilters().AnyAsync(c => c.Id == carId)).Should().BeTrue();
    }
}
