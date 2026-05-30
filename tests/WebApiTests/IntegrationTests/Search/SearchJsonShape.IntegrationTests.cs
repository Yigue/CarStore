using Domain.Clients.Attributes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Search;

public class SearchJsonShapeIntegrationTests
{
    [Fact]
    public async Task SearchClients_Returns_Status_As_String()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/clients/search?q=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var jsonArray = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        jsonArray.ValueKind.Should().Be(JsonValueKind.Array);
        
        if (jsonArray.GetArrayLength() > 0)
        {
            var firstClient = jsonArray[0];
            
            firstClient.TryGetProperty("status", out var statusValue).Should().BeTrue("Property 'status' should exist in the JSON response");
            statusValue.ValueKind.Should().Be(JsonValueKind.String, "Property 'status' should be a string (enum converted)");
            statusValue.GetString().Should().NotBeNullOrEmpty();
        }
    }
}
