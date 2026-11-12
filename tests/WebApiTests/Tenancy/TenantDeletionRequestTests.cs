using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.DealerSettings;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Tenancy;

/// <summary>RN-015 (A23): <c>POST /api/v1/dealer/deletion-request</c> needs the password and the slug typed by hand.</summary>
public class TenantDeletionRequestTests
{
    private static readonly Guid AdminDealer = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
    // Slug of the dealer created by DealerSettingsSeeder.
    private const string Slug = "lux";

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client)> CreateHostAsync(bool authenticated = true)
    {
        var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();
        if (authenticated)
        {
            IntegrationTestHelpers.SetAuthToken(client, await IntegrationTestHelpers.GetAdminTokenAsync(factory));
        }

        return (factory, client);
    }

    /// <summary>There is no verification backfill: the seeded owner starts unverified, so tests that need it verify it explicitly.</summary>
    private static async Task VerifyOwnerAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid ownerId = (await db.DealerSettings.IgnoreQueryFilters().SingleAsync(d => d.DealerId == AdminDealer)).OwnerUserId!.Value;
        var owner = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == ownerId);
        owner.MarkEmailVerified(DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private static async Task<TenantLifecycleStatus> LifecycleAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.DealerSettings.IgnoreQueryFilters().SingleAsync(d => d.DealerId == AdminDealer)).LifecycleStatus;
    }

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        var (factory, client) = await CreateHostAsync(authenticated: false);
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/v1/dealer/deletion-request", new { password = "x", confirmationSlug = Slug });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WrongPassword_Returns403_AndNothingChanges()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/v1/dealer/deletion-request", new { password = "nope", confirmationSlug = Slug });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await LifecycleAsync(factory)).Should().Be(TenantLifecycleStatus.Active);
    }

    [Fact]
    public async Task MissingConfirmation_Returns400_AndNothingChanges()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/v1/dealer/deletion-request", new { password = "Admin123!", confirmationSlug = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LifecycleAsync(factory)).Should().Be(TenantLifecycleStatus.Active);
    }

    [Fact]
    public async Task WrongSlug_Returns400_AndNothingChanges()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/v1/dealer/deletion-request", new { password = "Admin123!", confirmationSlug = "other-dealer" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LifecycleAsync(factory)).Should().Be(TenantLifecycleStatus.Active);
    }

    [Fact]
    public async Task OwnerWithUnverifiedEmail_Returns403_AndNothingChanges()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/v1/dealer/deletion-request", new { password = "Admin123!", confirmationSlug = Slug });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("TenantDeletion.EmailNotVerified");
        (await LifecycleAsync(factory)).Should().Be(TenantLifecycleStatus.Active);
    }

    [Fact]
    public async Task Confirmed_EntersRetention_WithoutDeletingTheAccount()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;
        await VerifyOwnerAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/dealer/deletion-request", new { password = "Admin123!", confirmationSlug = Slug });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await LifecycleAsync(factory)).Should().Be(TenantLifecycleStatus.CancelledGrace);
    }
}
