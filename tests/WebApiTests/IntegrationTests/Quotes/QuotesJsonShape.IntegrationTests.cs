using Domain.Quotes;
using Domain.Quotes.Attributes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Quotes;

public class QuotesJsonShapeIntegrationTests
{
    [Fact]
    public async Task GetQuotes_Returns_Status_As_String()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/quotes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var jsonArray = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        jsonArray.ValueKind.Should().Be(JsonValueKind.Array);
        jsonArray.GetArrayLength().Should().BeGreaterThan(0);

        var firstQuote = jsonArray[0];
        
        firstQuote.TryGetProperty("status", out var statusValue).Should().BeTrue("Property 'status' should exist in the JSON response");
        statusValue.ValueKind.Should().Be(JsonValueKind.String, "Property 'status' should be a string (enum converted)");
        statusValue.GetString().Should().NotBeNullOrEmpty();
    }
}
