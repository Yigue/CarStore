using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Postgres;

/// <summary>
/// CreateUserCommandHandler's email-uniqueness check — <c>u.Email.Value.ToLower() == emailValue</c>
/// — never translates against Postgres: EF Core throws InvalidOperationException at the LINQ
/// compilation step (not a DomainException), so it falls through the pipeline's generic handler
/// as a 500. Every call to POST /users failed this way, for any role, on real Postgres.
///
/// The rest of the suite runs on TestApplicationDbContext's InMemory provider, which evaluates
/// LINQ client-side and never sees a translation failure — this class exists because it is the
/// one place that runs against a real Postgres (via Testcontainers) and can actually catch it.
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class CreateUserPostgresTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public CreateUserPostgresTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private sealed record LoginResponse(string Token);

    private async Task<(HttpClient Client, Guid RoleId)> AdminClientWithARealRoleAsync()
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/users/login",
            new { Email = "admin@carstore.com", Password = "Admin123!" },
            IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        IntegrationTestHelpers.SetAuthToken(client, login!.Token);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Seeded by DatabaseSeeder/UsersSeeder — this IS the row GET /roles is meant to surface.
        var roleId = await context.Roles
            .Where(r => r.DealerId == dealerId)
            .Select(r => r.Id)
            .FirstAsync();

        return (client, roleId);
    }

    [Fact]
    public async Task CreateUser_Succeeds_WithARealRoleGuid_AgainstRealPostgres()
    {
        var (client, roleId) = await AdminClientWithARealRoleAsync();

        var response = await client.PostAsJsonAsync("/api/v1/users", new
        {
            Email = "new-hire-success@example.com",
            Password = "Password1!",
            FirstName = "New",
            LastName = "Hire",
            Role = roleId.ToString(),
        });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"body was: {body}");
    }

    [Fact]
    public async Task CreateUser_ReportsDuplicateEmail_InsteadOfCrashing()
    {
        var (client, roleId) = await AdminClientWithARealRoleAsync();
        var payload = new
        {
            Email = "dup-target@example.com",
            Password = "Password1!",
            FirstName = "Dup",
            LastName = "Target",
            Role = roleId.ToString(),
        };

        var first = await client.PostAsJsonAsync("/api/v1/users", payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/users", payload);

        second.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }
}
