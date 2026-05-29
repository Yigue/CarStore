using Domain.Cars;
using Domain.Cars.Attributes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApiTests.IntegrationTests.Cars;

public class CarsJsonShapeIntegrationTests
{
    [Fact]
    public async Task GetCars_Returns_Status_As_String()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/cars");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);

        var firstCar = items[0];
        
        // Assert property names and types
        firstCar.TryGetProperty("status", out var statusValue).Should().BeTrue("Property 'status' should exist in the JSON response");
        statusValue.ValueKind.Should().Be(JsonValueKind.String, "Property 'status' should be a string (enum converted)");
        statusValue.GetString().Should().NotBeNullOrEmpty();
        
        firstCar.TryGetProperty("serviceStatus", out var serviceStatusValue).Should().BeTrue("Property 'serviceStatus' should exist in the JSON response");
        serviceStatusValue.ValueKind.Should().Be(JsonValueKind.String, "Property 'serviceStatus' should be a string (enum converted)");
        serviceStatusValue.GetString().Should().NotBeNullOrEmpty();
    }
}
