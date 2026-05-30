using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// REQ-VMS-2/4/6/7 — end-to-end image flow against the in-memory FakeStorageService
/// (no Testcontainers/Docker required; see verification policy).
/// </summary>
public class CarImagesFlowTests
{
    private static async Task<Guid> SeedCarAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var brand = await context.Marca.IgnoreQueryFilters().FirstAsync(m => m.Nombre == "Toyota");
        var model = await context.Modelo.IgnoreQueryFilters()
            .FirstAsync(m => m.Nombre == "Corolla" && m.MarcaId == brand.Id);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var car = new Car(dealerId, brand, model, Color.Blue, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 2000, 0, 2024,
            $"AAA{Random.Shared.Next(100, 999)}", "img test", 25000m, DateTime.UtcNow);

        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car.Id;
    }

    private static MultipartFormDataContent ImageContent(string fileName = "photo.jpg", string contentType = "image/jpeg")
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8 };
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { fileContent, "file", fileName } };
        return form;
    }

    private sealed record UploadedImage(Guid Id, string Url, bool IsCover, int DisplayOrder);

    private static async Task<UploadedImage> UploadAsync(HttpClient client, Guid carId)
    {
        var response = await client.PostAsync($"/api/v1/cars/{carId}/images", ImageContent());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<UploadedImage>();
        return dto!;
    }

    [Fact]
    public async Task FullFlow_Upload_List_Reorder_SetCover_Delete()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        Guid carId = await SeedCarAsync(factory);

        // 1. Upload 3 images.
        var img1 = await UploadAsync(client, carId);
        var img2 = await UploadAsync(client, carId);
        var img3 = await UploadAsync(client, carId);

        factory.Storage.Count.Should().Be(3);

        // 2. GET list → 3 items ordered 0,1,2; all URLs use the public host.
        var list = await GetImagesAsync(client, carId);
        list.Should().HaveCount(3);
        list.Select(i => i.DisplayOrder).Should().Equal(0, 1, 2);
        list.All(i => i.Url.Contains("localhost:9000")).Should().BeTrue();
        list.All(i => !i.Url.Contains("minio:9000")).Should().BeTrue();

        // 3. Reorder to [id3, id1, id2].
        var reorder = await client.PutAsJsonAsync($"/api/v1/cars/{carId}/images/reorder",
            new { orderedImageIds = new[] { img3.Id, img1.Id, img2.Id } });
        reorder.StatusCode.Should().Be(HttpStatusCode.NoContent);

        list = await GetImagesAsync(client, carId);
        list.Select(i => i.Id).Should().Equal(img3.Id, img1.Id, img2.Id);

        // 4. PATCH cover on img2 → exactly img2 is cover.
        var cover = await client.PatchAsync($"/api/v1/cars/{carId}/images/{img2.Id}/cover", null);
        cover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        list = await GetImagesAsync(client, carId);
        list.Count(i => i.IsCover).Should().Be(1);
        list.Single(i => i.IsCover).Id.Should().Be(img2.Id);

        // 5. DELETE img1 → 2 items remain, blob gone from storage.
        var del = await client.DeleteAsync($"/api/v1/cars/{carId}/images/{img1.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        list = await GetImagesAsync(client, carId);
        list.Should().HaveCount(2);
        list.Should().NotContain(i => i.Id == img1.Id);
        factory.Storage.Count.Should().Be(2);
    }

    [Fact]
    public async Task Reorder_WithUnknownId_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        Guid carId = await SeedCarAsync(factory);
        var img1 = await UploadAsync(client, carId);

        var reorder = await client.PutAsJsonAsync($"/api/v1/cars/{carId}/images/reorder",
            new { orderedImageIds = new[] { img1.Id, Guid.NewGuid() } });

        reorder.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_WithoutPermission_OrAnonymous_Returns401Or403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(); // no auth token
        Guid carId = await SeedCarAsync(factory);

        var response = await client.PostAsync($"/api/v1/cars/{carId}/images", ImageContent());
        response.StatusCode.Should().Match(s =>
            s == HttpStatusCode.Unauthorized || s == HttpStatusCode.Forbidden);
    }

    private static async Task<List<UploadedImage>> GetImagesAsync(HttpClient client, Guid carId)
    {
        var response = await client.GetAsync($"/api/v1/cars/{carId}/images");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("items");
        var result = new List<UploadedImage>();
        foreach (var item in items.EnumerateArray())
        {
            result.Add(new UploadedImage(
                item.GetProperty("id").GetGuid(),
                item.GetProperty("url").GetString()!,
                item.GetProperty("isCover").GetBoolean(),
                item.GetProperty("displayOrder").GetInt32()));
        }
        return result;
    }
}
