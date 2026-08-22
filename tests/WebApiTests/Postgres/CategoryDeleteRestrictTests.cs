using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Financial;
using Domain.Financial.Attributes;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Postgres;

/// <summary>
/// qa-p1-integridad PR2, Slice 4 (D3, REQ: finance-category-referential-integrity).
/// <para>
/// Today <c>transactions.category_id</c> defaults to EF's <c>Cascade</c> — the only one of four
/// <see cref="FinancialTransaction"/> FKs not explicitly restricted — so deleting a referenced
/// category returns 204 and destroys every transaction that referenced it. These tests can only
/// run against real Postgres: SQLite/in-memory cannot observe an FK's delete rule.
/// </para>
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class CategoryDeleteRestrictTests : IAsyncLifetime
{
    private const string AdminDealerId = "11111111-1111-1111-1111-111111111111";

    private readonly PostgresWebApplicationFactory _factory;

    public CategoryDeleteRestrictTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<string> GetAdminTokenAsync()
    {
        var client = _factory.CreateClient();
        var loginRequest = new { Email = "admin@carstore.com", Password = "Admin123!" };

        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", loginRequest, IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();

        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        return result!.Token;
    }

    private sealed record LoginResponse(string Token);

    private static async Task<Guid> CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/financial/categories",
            new { Name = name, Description = "qa-p1-integridad category delete restrict test", Type = TransactionType.Expense },
            IntegrationTestHelpers.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(IntegrationTestHelpers.JsonOptions);
    }

    private static async Task<Guid> CreateTransactionAsync(HttpClient client, Guid categoryId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/financial",
            new
            {
                type = (int)TransactionType.Expense,
                amount = 100m,
                description = "qa-p1-integridad category delete restrict test",
                paymentMethod = (int)PaymentMethod.Cash,
                categoryId,
                carId = (Guid?)null,
                clientId = (Guid?)null,
                saleId = (Guid?)null
            },
            IntegrationTestHelpers.JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task DeleteCategory_ReferencedByTransactions_Returns409AndDestroysNothing()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var categoryId = await CreateCategoryAsync(client, "In Use " + Guid.NewGuid());
        await CreateTransactionAsync(client, categoryId);
        await CreateTransactionAsync(client, categoryId);
        await CreateTransactionAsync(client, categoryId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countBefore = await db.Transactions.IgnoreQueryFilters().CountAsync(t => t.CategoryId == categoryId);
        countBefore.Should().Be(3);

        var response = await client.DeleteAsync($"/api/v1/financial/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a category referenced by transactions must never be destroyed via 204/cascade");

        var raw = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<JsonElement>(raw);
        problem.GetProperty("title").GetString().Should().Be("Category.InUse");
        problem.GetProperty("detail").GetString().Should().Contain("3");

        var countAfter = await db.Transactions.IgnoreQueryFilters().CountAsync(t => t.CategoryId == categoryId);
        countAfter.Should().Be(countBefore, "no transaction row may be deleted or mutated as a side effect of a blocked category delete");

        var categoryStillExists = await db.TransactionCategories.AnyAsync(c => c.Id == categoryId);
        categoryStillExists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCategory_NoReferencingTransactions_Returns204()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var categoryId = await CreateCategoryAsync(client, "Unused " + Guid.NewGuid());

        var response = await client.DeleteAsync($"/api/v1/financial/categories/{categoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stillExists = await db.TransactionCategories.AnyAsync(c => c.Id == categoryId);
        stillExists.Should().BeFalse();
    }

    [Fact]
    public async Task TransactionCategoryForeignKey_IsRestrictNotCascade()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // confdeltype: 'r' = RESTRICT/NO ACTION on delete, 'c' = CASCADE.
        var confDelType = await db.Database
            .SqlQueryRaw<string>(
                "SELECT confdeltype::text AS \"Value\" FROM pg_constraint " +
                "WHERE conname = 'fk_transactions_transaction_categories_category_id'")
            .SingleAsync();

        confDelType.Should().Be("r", "the migration must arm ON DELETE RESTRICT at the schema level, independent of the handler guard");
    }

    [Fact]
    public async Task DeleteCategory_ConcurrentInsertDuringDelete_Returns409NotFiveHundred()
    {
        var token = await GetAdminTokenAsync();
        var client = _factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var categoryId = await CreateCategoryAsync(client, "Race " + Guid.NewGuid());

        using var raceScope = _factory.Services.CreateScope();
        var raceDb = raceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Session A: open an explicit transaction and insert a transaction referencing the
        // category, but do NOT commit yet. Postgres takes a FOR KEY SHARE lock on the category
        // row as part of the INSERT — held until this transaction ends. Because it is
        // uncommitted, Session B's READ COMMITTED pre-check (AnyAsync) will NOT see it, so the
        // handler's guard passes and reaches the real DELETE, which then blocks on this lock
        // instead of failing immediately.
        await using var raceTransaction = await raceDb.Database.BeginTransactionAsync();
        var categoryForRaceWrite = await raceDb.TransactionCategories.SingleAsync(c => c.Id == categoryId);
        var racingTransaction = new FinancialTransaction(
            Guid.Parse(AdminDealerId),
            TransactionType.Expense,
            50m,
            "qa-p1-integridad concurrent-insert race test",
            PaymentMethod.Cash,
            categoryForRaceWrite);
        raceDb.Transactions.Add(racingTransaction);
        await raceDb.SaveChangesAsync();

        var deleteTask = client.DeleteAsync($"/api/v1/financial/categories/{categoryId}");

        // Give Session B's HTTP request time to pass the AnyAsync pre-check (which sees
        // nothing, since Session A hasn't committed) and reach the blocking DELETE.
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await raceTransaction.CommitAsync();

        var response = await deleteTask;

        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "the concurrent insert must surface as 409 Category.InUse via the DbUpdateException/23503 catch, never as an unhandled 500");

        var raw = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<JsonElement>(raw);
        problem.GetProperty("title").GetString().Should().Be("Category.InUse");
    }
}
