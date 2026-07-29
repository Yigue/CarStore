using Domain.Sales;
using Domain.Sales.Attributes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Sales;

public class SalesJsonShapeIntegrationTests
{
    [Fact]
    public async Task GetSales_Returns_Status_As_String()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/sales");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var jsonArray = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        jsonArray.ValueKind.Should().Be(JsonValueKind.Array);
        jsonArray.GetArrayLength().Should().BeGreaterThan(0);

        var firstSale = jsonArray[0];
        
        firstSale.TryGetProperty("status", out var statusValue).Should().BeTrue("Property 'status' should exist in the JSON response");
        statusValue.ValueKind.Should().Be(JsonValueKind.String, "Property 'status' should be a string (enum converted)");
        statusValue.GetString().Should().NotBeNullOrEmpty();
    }

    // REQ-SL-XREF-001: the frontend reads quoteId/leadId off the sales payload to
    // render the Lead/Quote cross-navigation links in SaleDetailsModal. SaleResponse
    // declares both, but a stray [JsonIgnore] or a null-omitting serializer policy
    // would silently drop them from the wire and break the links without any
    // compile-time signal. This pins the contract.
    [Fact]
    public async Task GetSales_Serializes_QuoteId_And_LeadId()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/sales");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonArray = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        jsonArray.ValueKind.Should().Be(JsonValueKind.Array);
        jsonArray.GetArrayLength().Should().BeGreaterThan(0);

        var firstSale = jsonArray[0];

        firstSale.TryGetProperty("quoteId", out var quoteId)
            .Should().BeTrue("Property 'quoteId' must be serialized so the FE can link to the originating quote");
        quoteId.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);

        firstSale.TryGetProperty("leadId", out var leadId)
            .Should().BeTrue("Property 'leadId' must be serialized so the FE can link to the originating lead");
        leadId.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);
    }

    [Fact]
    public async Task GetSaleById_Serializes_QuoteId_And_LeadId()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var listResponse = await client.GetAsync("/api/v1/sales");
        var jsonArray = await listResponse.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        var saleId = jsonArray[0].GetProperty("id").GetString();

        // Act
        var response = await client.GetAsync($"/api/v1/sales/{saleId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sale = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);

        sale.TryGetProperty("quoteId", out var quoteId)
            .Should().BeTrue("Property 'quoteId' must be serialized on the detail endpoint too");
        quoteId.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);

        sale.TryGetProperty("leadId", out var leadId)
            .Should().BeTrue("Property 'leadId' must be serialized on the detail endpoint too");
        leadId.ValueKind.Should().BeOneOf(JsonValueKind.String, JsonValueKind.Null);
    }
}
