using System.Net;
using System.Net.Http.Json;
using Domain.Billing;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApiTests.Fakes;

namespace WebApiTests.Platform;

/// <summary>
/// Integration tests for platform endpoints (PR1, task 1.5.7).
/// Verifies: SuperAdmin can access platform endpoints; tenant Admin cannot; unauthenticated gets 401.
/// </summary>
public class EndpointsTests
{
    [Fact]
    public async Task GetPlatformDealers_AsSuperAdmin_Returns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var token = await IntegrationTestHelpers.GetSuperAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/platform/dealers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPlatformDealers_AsTenantAdmin_Returns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/platform/dealers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPlatformDealers_Unauthenticated_Returns401()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/platform/dealers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPlatformMetrics_AsSuperAdmin_Returns200()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var token = await IntegrationTestHelpers.GetSuperAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/platform/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MetricsBody>(IntegrationTestHelpers.JsonOptions);
        body.Should().NotBeNull();

        // spr PR 1.3: MRR is computed from DealerSubscription × the plan price, no longer a stub.
        // The price comes from FakePlanCatalog, so the expected figure is exact.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var billable = await db.DealerSubscriptions
            .IgnoreQueryFilters()
            .CountAsync(s => s.Status == SubscriptionStatus.Active
                || s.Status == SubscriptionStatus.Trialing
                || s.Status == SubscriptionStatus.PastDue);

        body!.Mrr.IsStub.Should().BeFalse();
        body.Mrr.StubReason.Should().BeNull();
        body.Mrr.Value.Should().Be(billable * (FakePlanCatalog.AmountMinor / 100m));
    }

    [Fact]
    public async Task GetPlatformMetrics_AsTenantAdmin_Returns403()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/platform/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record MetricsBody(int TotalDealers, int TotalUsers, MrrStubBody Mrr);
    private sealed record MrrStubBody(decimal Value, string Currency, bool IsStub, string? StubReason);
}
