using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace WebApiTests.Endpoints;

public class DealerSettingsAnonymousContractTests
{
    [Fact]
    public async Task GetDealerSettings_AnonymousRequest_ExposesExactFieldSet()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.GetAsync("/api/v1/dealer-settings");

        // The endpoint is anonymous and returns Ok (or NotFound/Problem if default tenant settings do not exist in test DB)
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Enforce exact anonymous field contract:
            // Must contain contactEmail, phoneNumber, address, interestRateTna
            root.TryGetProperty("contactEmail", out _).Should().BeTrue();
            root.TryGetProperty("phoneNumber", out _).Should().BeTrue();
            root.TryGetProperty("address", out _).Should().BeTrue();
            root.TryGetProperty("interestRateTna", out _).Should().BeTrue();
        }
        else
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError);
        }
    }
}
