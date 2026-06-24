using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.IntegrationTests.Cars.Images;

/// <summary>REQ-VMS-4 / ADR-4 — presigned URLs returned to clients never expose the internal host.</summary>
public class PresignedUrlRewriterE2ETests
{
    [Fact]
    public async Task UploadedImage_PresignedUrls_NeverContainInternalHost()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var brand = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Toyota");
            var model = await context.Modelo.IgnoreQueryFilters()
                .FirstAsync(m => m.Nombre == "Corolla" && m.MarcaId == brand.Id);
            var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
            var car = new Car(dealerId, brand, model, Color.Black, TypeCar.Sedan, StatusCar.New,
                StatusServiceCar.Disponible, 4, 5, 2000, 0, 2024,
                $"AAA{Random.Shared.Next(100, 999)}", "url test", 20000m, DateTime.UtcNow);
            context.Cars.Add(car);
            await context.SaveChangesAsync();
            _carId = car.Id;
        }

        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var form = new MultipartFormDataContent { { fileContent, "file", "x.jpg" } };

        var upload = await client.PostAsync($"/api/v1/cars/{_carId}/images", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await client.GetAsync($"/api/v1/cars/{_carId}/images");
        var raw = await listResponse.Content.ReadAsStringAsync();

        raw.Should().NotContain("minio:9000");
        raw.Should().Contain("localhost:9000");
    }

    private Guid _carId;
}
