using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace WebApiTests.IntegrationTests.Cars;

public class BrandModelsSeedIntegrationTests
{
    [Fact]
    public async Task GetModelos_Returns_SeededModels_ForNewBrands()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Honda, Fiat, Renault, Peugeot
        var newBrandIds = new[]
        {
            new Guid("84092836-c9b3-4cf3-97be-488efe9ace6c"), // Honda
            new Guid("f6b5ad95-2da3-44bd-b18b-b686cb4f95e2"), // Fiat
            new Guid("ebed17d2-dc33-4d2a-ae32-a17cca75536b"), // Renault
            new Guid("3a2b2917-6580-4b74-8304-bb6ff4febcf1")  // Peugeot
        };

        foreach (var brandId in newBrandIds)
        {
            // Act
            var response = await client.GetAsync($"/api/v1/modelos/marca/{brandId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"Brand {brandId} models should exist");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
            json.ValueKind.Should().Be(JsonValueKind.Array);
            json.GetArrayLength().Should().BeGreaterThan(0, $"Brand {brandId} should return > 0 models");
        }
    }
}
