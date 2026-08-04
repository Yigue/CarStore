using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Clients.GetAll;
using FluentAssertions;
using Xunit;

namespace WebApiTests.Postgres;

/// <summary>
/// qa-p0-blockers C1 regression guard: proves accent-insensitive client search executes in the
/// database via the `search_name` STORED generated column (D1 superseded, decision 2026-08-03) and
/// that filtering/pagination are pushed down to SQL, instead of an unbounded full-table load
/// followed by in-process C# filtering.
///
/// History: these two cases were originally <see cref="Fact.Skip"/>ped because the D1-mandated
/// approach -- <c>EF.Functions.Collate(c.FirstName + " " + c.LastName, "und-u-ks-primary").Contains(...)</c>
/// -- reproduces a real, permanent PostgreSQL error against a live postgres:17-alpine container:
/// "0A000: nondeterministic collations are not supported for LIKE" (nondeterministic ICU collations
/// only ever support equality/ordering, never pattern matching, at any version). The replacement
/// design filters via `search_name = lower(f_unaccent(first_name || ' ' || last_name))`, a STORED
/// generated column pinned to a deterministic "C" collation and backed by a GIN trigram index, so
/// `LIKE` works and these assertions are satisfiable for real.
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class PostgresClientSearchSqlPushdownTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresClientSearchSqlPushdownTests(PostgresFixture fixture)
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
    public async Task SearchClients_ShouldExecuteSearchNameFilterAndLimitInDatabase()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        _factory.CommandInterceptor.Clear();

        var response = await client.GetAsync("/api/v1/clients/search?q=jose");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var err = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Response error: {err}");
        }

        var commands = _factory.CommandInterceptor.CommandTexts;
        commands.Should().NotBeEmpty();

        commands.Should().Contain(
            sql => sql.Contains("search_name", StringComparison.OrdinalIgnoreCase),
            because: "the accent-insensitive match must be evaluated by Postgres via the " +
                     "search_name generated column, not reproduced in application memory after " +
                     "a full-table load");

        commands.Should().Contain(
            sql => sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase),
            because: "the 50-row cap must be enforced by the database (LIMIT), not by truncating an " +
                     "in-memory List<Client> after loading every matching row");
    }

    [Fact]
    public async Task GetAllClients_WithSearch_ShouldExecuteSearchNameFilterAndPaginationInDatabase()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        _factory.CommandInterceptor.Clear();

        var response = await client.GetAsync("/api/v1/clients?search=jose&page=1&pageSize=5");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var err = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Response error: {err}");
        }

        var commands = _factory.CommandInterceptor.CommandTexts;
        commands.Should().NotBeEmpty();

        commands.Should().Contain(
            sql => sql.Contains("search_name", StringComparison.OrdinalIgnoreCase),
            because: "the search filter must be evaluated by Postgres via the search_name generated " +
                     "column, not reproduced in application memory after a full-table load");

        commands.Should().Contain(
            sql => sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)
                   && sql.Contains("OFFSET", StringComparison.OrdinalIgnoreCase),
            because: "pagination must be applied by the database (LIMIT/OFFSET translated from " +
                     "Skip/Take), not by paginating an in-memory list after loading every matching row");
    }

    [Fact]
    public async Task SearchClients_TermUnaccentedInDatabase_MatchesStoredAccentedName()
    {
        // Trap 1/2 regression guard: "jose" (no diacritics) must match a stored "José" via the
        // search_name generated column, and the TERM itself must be unaccented in SQL
        // (f_unaccent), not in .NET -- proving both the collation-propagation trap (search_name
        // is queryable with LIKE at all) and the term/column symmetry trap (the term-side
        // f_unaccent call appears in the generated SQL) are actually closed, not just documented.
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        _factory.CommandInterceptor.Clear();

        var response = await client.GetAsync("/api/v1/clients/search?q=jose");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var clients = await response.Content.ReadFromJsonAsync<List<ClientResponse>>(IntegrationTestHelpers.JsonOptions);
        clients.Should().Contain(c => c.FirstName == "José");

        var commands = _factory.CommandInterceptor.CommandTexts;
        commands.Should().Contain(
            sql => sql.Contains("f_unaccent", StringComparison.OrdinalIgnoreCase),
            because: "the search TERM must be unaccented via the same Postgres f_unaccent " +
                     "dictionary that produced the stored search_name column (term/column " +
                     "symmetry), not via .NET FormD normalization which disagrees with Postgres " +
                     "unaccent on some characters");
    }
}
