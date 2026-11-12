using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Domain.Billing;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Database.SeedData;
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

        // The Postgres collection shares one database across test classes, so other tests may have
        // added dealers of their own: only the dealers the seed is responsible for count here.
        var seededDealerIds = SubscriptionStateSeed.AdditionalDealers
            .Select(d => d.DealerId)
            .Append(Guid.Parse("11111111-1111-1111-1111-111111111111"))
            .ToHashSet();

        var subscriptions = (await db.DealerSubscriptions
            .IgnoreQueryFilters()
            .ToListAsync())
            .Where(s => seededDealerIds.Contains(s.DealerId))
            .ToList();

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

    // qa-p0-blockers C2. The spec requires each seeded dealer's admin to be independently able
    // to authenticate, but the only coverage was "a row exists", which a literal placeholder
    // hash satisfies. Verify() hex-decodes the stored value, so a placeholder throws on every
    // login instead of returning false — asserting the row exists could never catch that.
    [Fact]
    public async Task SeededDealerAdmins_ShouldHaveVerifiableCredentials()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var seededEmails = SubscriptionStateSeed.AdditionalDealers.Select(d => d.Email).ToList();
        seededEmails.Should().NotBeEmpty();

        var admins = await db.Users
            .IgnoreQueryFilters()
            .Where(u => seededEmails.Contains(u.Email))
            .ToListAsync();

        admins.Should().HaveCount(seededEmails.Count);

        foreach (var admin in admins)
        {
            passwordHasher
                .Invoking(h => h.Verify("Admin123!", admin.PasswordHash))
                .Should().NotThrow($"{admin.Email} must have a real hash, not a placeholder");

            passwordHasher.Verify("Admin123!", admin.PasswordHash)
                .Should().BeTrue($"{admin.Email} must be able to authenticate with the seeded password");
        }
    }
}
