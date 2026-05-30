using Domain.Leads;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Leads;

public class LeadsJsonShapeIntegrationTests
{
    [Fact]
    public async Task GetLeads_Returns_Status_And_Source_As_String()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Leads.Add(Domain.Leads.Lead.Create(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Test Client",
                "test@lead.com",
                "123456",
                Domain.Leads.LeadSource.Web,
                DateTime.UtcNow
            ));
            await db.SaveChangesAsync();
        }

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/leads");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var jsonArray = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        jsonArray.ValueKind.Should().Be(JsonValueKind.Array);
        jsonArray.GetArrayLength().Should().BeGreaterThan(0);

        var firstLead = jsonArray[0];
        
        firstLead.TryGetProperty("status", out var statusValue).Should().BeTrue("Property 'status' should exist in the JSON response");
        statusValue.ValueKind.Should().Be(JsonValueKind.String, "Property 'status' should be a string (enum converted)");
        statusValue.GetString().Should().NotBeNullOrEmpty();
        
        firstLead.TryGetProperty("source", out var sourceValue).Should().BeTrue("Property 'source' should exist in the JSON response");
        sourceValue.ValueKind.Should().Be(JsonValueKind.String, "Property 'source' should be a string (enum converted)");
        sourceValue.GetString().Should().NotBeNullOrEmpty();
    }
}
