using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using WebApiTests.Postgres;
using Xunit;

namespace WebApiTests.Security;

[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class WebhookSsrfTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public WebhookSsrfTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private sealed record LoginResponse(string Token);

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/users/login",
            new { Email = "admin@carstore.com", Password = "Admin123!" },
            IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        IntegrationTestHelpers.SetAuthToken(client, login!.Token);
        return client;
    }

    [Theory]
    [InlineData("http://127.0.0.1/webhook")]
    [InlineData("http://127.0.0.2:8080/hook")]
    [InlineData("http://localhost/webhook")]
    [InlineData("http://10.0.0.1/webhook")]
    [InlineData("http://10.255.0.1:9000/callback")]
    [InlineData("http://172.16.0.1/webhook")]
    [InlineData("http://172.31.255.255/webhook")]
    [InlineData("http://192.168.1.1/webhook")]
    [InlineData("http://192.168.100.50:5000/hook")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/webhook")]
    [InlineData("http://[fe80::1]/webhook")]
    public async Task CreateWebhook_BlockedSsrfRanges_Should_Return400BadRequest(string blockedUrl)
    {
        var client = await CreateAdminClientAsync();

        var payload = new
        {
            Url = blockedUrl,
            EventTypes = new[] { "CarCreated" }
        };

        var response = await client.PostAsJsonAsync("/api/v1/webhooks", payload, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: $"SSRF target '{blockedUrl}' must be rejected with 400 Bad Request");
    }
}
