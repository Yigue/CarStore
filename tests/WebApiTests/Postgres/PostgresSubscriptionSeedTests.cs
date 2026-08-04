using System.Linq;
using System.Threading.Tasks;
using Domain.Billing;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Postgres;

[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class PostgresSubscriptionSeedTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresSubscriptionSeedTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SeedData_ShouldContainAllFiveSubscriptionStates_AcrossDistinctDealers()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var subscriptions = await db.DealerSubscriptions
            .IgnoreQueryFilters()
            .ToListAsync();

        subscriptions.Select(s => s.DealerId).Distinct().Should().HaveCount(5);
        subscriptions.Select(s => s.Status).Should().Contain(new[]
        {
            SubscriptionStatus.Active,
            SubscriptionStatus.Trialing,
            SubscriptionStatus.PastDue,
            SubscriptionStatus.Suspended,
            SubscriptionStatus.Cancelled
        });
    }
}
