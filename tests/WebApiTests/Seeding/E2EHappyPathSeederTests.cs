using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Domain.Billing;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Database.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiTests.Seeding;

/// <summary>spr 8.1.5: the happy-path fixtures exist, are idempotent, and cannot run outside Development/Testing.</summary>
public class E2EHappyPathSeederTests
{
    private static IConfiguration Config(bool enabled) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [E2EHappyPathSeeder.ConfigKey] = enabled.ToString() })
        .Build();

    private static async Task RunAsync(CustomWebApplicationFactory factory, bool enabled, string environment = "Testing")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await E2EHappyPathSeeder.SeedAsync(
            db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), Config(enabled), environment);
    }

    [Fact]
    public async Task Disabled_DoesNothing()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        await RunAsync(factory, enabled: false);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == E2EHappyPathSeeder.ResetUserEmail)).Should().BeFalse();
        (await db.DealerSettings.IgnoreQueryFilters().AnyAsync(s => s.DealerId == E2EHappyPathSeeder.SuspendDealerId)).Should().BeFalse();
    }

    [Fact]
    public async Task Enabled_SeedsEveryFixture_AndIsIdempotent()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        await RunAsync(factory, enabled: true);
        await RunAsync(factory, enabled: true);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var settings = await db.DealerSettings.IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == E2EHappyPathSeeder.SuspendDealerId);
        settings.Slug.Should().Be(E2EHappyPathSeeder.SuspendDealerSlug);
        settings.IsActive.Should().BeTrue("HP-10 suspends it, so it has to start active");
        settings.OwnerUserId.Should().NotBeNull();

        (await db.DealerSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == E2EHappyPathSeeder.SuspendDealerId)).Status.Should().Be(SubscriptionStatus.Active);
        (await db.DealerSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == E2EHappyPathSeeder.DemoDealerId)).Status.Should().Be(SubscriptionStatus.Active);

        var admin = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == E2EHappyPathSeeder.SuspendAdminEmail);
        hasher.Verify("Admin123!", admin.PasswordHash).Should().BeTrue("the seeded admin must be able to log in");

        var reset = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == E2EHappyPathSeeder.ResetUserEmail);
        hasher.Verify(E2EHappyPathSeeder.ResetUserInitialPassword, reset.PasswordHash).Should().BeTrue();

        (await db.Cars.IgnoreQueryFilters().CountAsync(c => c.Patente == E2EHappyPathSeeder.VehiclePatente)).Should().Be(1);
        (await db.Leads.IgnoreQueryFilters().CountAsync(l => l.ClientName == E2EHappyPathSeeder.LeadName)).Should().Be(1);
    }

    [Fact]
    public async Task Rerun_RestoresTheResetUsersPassword()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        await RunAsync(factory, enabled: true);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == E2EHappyPathSeeder.ResetUserEmail);
            user.SetPassword(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("Changed-by-HP17!1"));
            await db.SaveChangesAsync();
        }

        await RunAsync(factory, enabled: true);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reset = await verifyDb.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == E2EHappyPathSeeder.ResetUserEmail);
        verifyScope.ServiceProvider.GetRequiredService<IPasswordHasher>()
            .Verify(E2EHappyPathSeeder.ResetUserInitialPassword, reset.PasswordHash).Should().BeTrue();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Enabled_OutsideDevelopmentOrTesting_Throws(string environment)
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        Func<Task> act = () => RunAsync(factory, enabled: true, environment);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*known password*");
    }

    [Fact]
    public async Task Disabled_OutsideDevelopmentOrTesting_DoesNotThrow()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        Func<Task> act = () => RunAsync(factory, enabled: false, "Production");

        await act.Should().NotThrowAsync();
    }
}
