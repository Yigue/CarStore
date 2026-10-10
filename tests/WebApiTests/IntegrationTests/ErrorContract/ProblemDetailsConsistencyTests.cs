using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApiTests.IntegrationTests.ErrorContract;

/// <summary>
/// API-008: error responses are RFC 7807 problem details (the shape OpenAPI documents), not
/// ad-hoc <c>{ "error": "..." }</c> bodies.
/// </summary>
public class ProblemDetailsConsistencyTests
{
    private static async Task AssertProblemAsync(HttpResponseMessage response, string expectedTitle)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = body.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("title").GetString().Should().Be(expectedTitle);
        root.TryGetProperty("type", out _).Should().BeTrue("problem details carry a type");
        root.TryGetProperty("error", out _).Should().BeFalse("the legacy ad-hoc error body is gone");
    }

    [Fact]
    public async Task Newsletter_InvalidEmail_ReturnsProblemDetails()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/newsletter/subscribe", new { email = "nope" });

        await AssertProblemAsync(response, "Newsletter.InvalidEmail");
    }

    [Fact]
    public async Task FinancialReport_MissingDateRange_ReturnsProblemDetails()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync("/api/v1/reports/financials?groupBy=Month");

        await AssertProblemAsync(response, "MISSING_DATE_RANGE");
    }

    [Fact]
    public async Task FinancialReport_InvalidGroupBy_ReturnsProblemDetails()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync("/api/v1/reports/financials?from=2050-01-01&to=2050-01-31&groupBy=Hour");

        await AssertProblemAsync(response, "INVALID_GROUP_BY");
    }
}
