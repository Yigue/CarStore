using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Clients.GetAll;
using Application.Clients.Projections;
using FluentAssertions;
using Xunit;

namespace WebApiTests.Postgres;

[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class PostgresCollationTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresCollationTests(PostgresFixture fixture)
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
    public async Task SearchClients_WithAccentInsensitiveQuery_ShouldReturnMatchingClient()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // 1. GET /api/v1/clients/search?q=jose
        var response1 = await client.GetAsync("/api/v1/clients/search?q=jose");
        if (response1.StatusCode != HttpStatusCode.OK)
        {
            var err = await response1.Content.ReadAsStringAsync();
            response1.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Response error: {err}");
        }
        var clients1 = await response1.Content.ReadFromJsonAsync<List<ClientResponse>>(IntegrationTestHelpers.JsonOptions);
        clients1.Should().Contain(c => c.FirstName == "José");

        // 2. GET /api/v1/clients?search=jose
        var response2 = await client.GetAsync("/api/v1/clients?search=jose");
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
