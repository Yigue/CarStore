using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace WebApiTests.Postgres;

/// <summary>
/// qa-p0-blockers C1 regression guard: intended to prove accent-insensitive client search executes
/// in the database via the `und-u-ks-primary` collation and that pagination/filtering are pushed
/// down to SQL, instead of an unbounded full-table load followed by in-process C# filtering.
///
/// BLOCKED (2026-08-03): both cases are currently <see cref="Fact.Skip"/>ped. Restoring
/// <c>EF.Functions.Collate(c.FirstName + " " + c.LastName, "und-u-ks-primary").Contains(...)</c>
/// against a live postgres:17-alpine container reproduces a real PostgreSQL error:
/// "0A000: nondeterministic collations are not supported for LIKE". This is not the PG-version
/// floor design D1 assumed ("non-deterministic collations only support LIKE/pattern matching from
/// PostgreSQL 17") -- it is a permanent PostgreSQL restriction: non-deterministic ICU collations
/// only support equality/ordering, never pattern matching, at any version. EF's `.Contains()`
/// against a `EF.Functions.Collate(...)` expression always compiles to
/// `... COLLATE "und-u-ks-primary" LIKE '%...%'`, so this architecture cannot back a substring
/// search, full stop. Fixing the underlying C1 performance regression (unbounded
/// materialization before pagination in both handlers) needs a design decision that supersedes
/// D1 -- e.g. the fallback design.md itself already floats: a `STORED` generated column
/// `lower(unaccent(first_name || ' ' || last_name))` + expression index, dropping the ICU
/// collation requirement for substring search. That is out of scope for this apply batch and is
/// reported back to the orchestrator instead of silently implemented. Un-skip these once a
/// design amendment lands.
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class PostgresClientSearchSqlPushdownTests : IAsyncLifetime
{
    private const string BlockedReason =
        "BLOCKED by qa-p0-blockers C1 architecture conflict: nondeterministic ICU collations do " +
        "not support LIKE in PostgreSQL (confirmed live against postgres:17-alpine: \"0A000: " +
        "nondeterministic collations are not supported for LIKE\"), so EF.Functions.Collate(...)" +
        ".Contains(...) can never satisfy this assertion. Needs a design decision superseding D1 " +
        "(e.g. generated column + unaccent) before this can be un-skipped.";

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

    [Fact(Skip = BlockedReason)]
    public async Task SearchClients_ShouldExecuteCollationAndLimitInDatabase()
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
            sql => sql.Contains("und-u-ks-primary", StringComparison.OrdinalIgnoreCase),
            because: "the accent-insensitive match must be evaluated by Postgres via the provisioned " +
                     "collation, not reproduced in application memory after a full-table load");

        commands.Should().Contain(
            sql => sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase),
            because: "the 50-row cap must be enforced by the database (LIMIT), not by truncating an " +
                     "in-memory List<Client> after loading every matching row");
    }

    [Fact(Skip = BlockedReason)]
    public async Task GetAllClients_WithSearch_ShouldExecuteCollationAndPaginationInDatabase()
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
            sql => sql.Contains("und-u-ks-primary", StringComparison.OrdinalIgnoreCase),
            because: "the search filter must be evaluated by Postgres via the provisioned collation, " +
                     "not reproduced in application memory after a full-table load");

        commands.Should().Contain(
            sql => sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)
                   && sql.Contains("OFFSET", StringComparison.OrdinalIgnoreCase),
            because: "pagination must be applied by the database (LIMIT/OFFSET translated from " +
                     "Skip/Take), not by paginating an in-memory list after loading every matching row");
    }
}
