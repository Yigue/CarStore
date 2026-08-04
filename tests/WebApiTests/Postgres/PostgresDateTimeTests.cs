using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace WebApiTests.Postgres;

[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class PostgresDateTimeTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresDateTimeTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var client = _factory.CreateClient();
        var loginRequest = new
        {
            Email = "admin@carstore.com",
            Password = "Admin123!"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", loginRequest, IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();

        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        return result!.Token;
    }

    private sealed record LoginResponse(string Token);

    [Fact]
    public async Task GetAppointments_WithDateOnlyAndOffsetShapes_ShouldReturn200()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Date-only shape
        var response1 = await client.GetAsync("/api/v1/appointments?from=2026-01-01&to=2026-12-31");
        if (response1.StatusCode != HttpStatusCode.OK)
        {
            var err1 = await response1.Content.ReadAsStringAsync();
            response1.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Response1 error: {err1}");
        }

        // ISO offset shape
        var response2 = await client.GetAsync("/api/v1/appointments?from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z");
        if (response2.StatusCode != HttpStatusCode.OK)
        {
            var err2 = await response2.Content.ReadAsStringAsync();
            response2.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Response2 error: {err2}");
        }
    }

    [Fact]
    public async Task GetFinancials_WithDateOnlyAndOffsetShapes_ShouldReturn200()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // ISO offset shape
        var response1 = await client.GetAsync("/api/v1/reports/financials?from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z&groupBy=day");
        if (response1.StatusCode != HttpStatusCode.OK)
        {
            var err1 = await response1.Content.ReadAsStringAsync();
            response1.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Financials response1 error: {err1}");
        }

        // Date-only shape
        var response2 = await client.GetAsync("/api/v1/reports/financials?from=2026-01-01&to=2026-12-31&groupBy=day");
        if (response2.StatusCode != HttpStatusCode.OK)
        {
            var err2 = await response2.Content.ReadAsStringAsync();
            response2.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Financials response2 error: {err2}");
        }
    }
}
