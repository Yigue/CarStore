using System.Net;
using System.Text;
using FluentAssertions;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Leads;

/// <summary>
/// Regression: the frontend serializes optional Guid fields as an empty string ("") rather than
/// omitting them or sending null. System.Text.Json cannot bind "" to a Guid?, which previously
/// surfaced as a 500 (BadHttpRequestException) before the handler ran. A nullable-Guid converter
/// must coerce ""/whitespace to null so the request binds cleanly.
/// </summary>
public class CreateLeadJsonToleranceIntegrationTests
{
    [Fact]
    public async Task CreateLead_WithEmptyStringInterestedVehicleId_DoesNotReturn500()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Raw JSON so we can force interestedVehicleId to an empty string, exactly like the client does.
        const string json = """
        {
            "clientName": "Empty Guid Lead",
            "email": "empty@lead.com",
            "phone": "123456",
            "source": "Web",
            "notes": null,
            "interestedVehicleId": ""
        }
        """;

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/leads", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
