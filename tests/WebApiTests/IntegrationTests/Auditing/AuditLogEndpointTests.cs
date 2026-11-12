using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Domain.Auditing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiTests.IntegrationTests.Auditing;

/// <summary>sfc B2.2.4 / A20: <c>GET /api/v1/audit-log?entity=&amp;from=&amp;to=</c>, guarded by <c>audit:read</c>, scoped to the caller's dealer.</summary>
public class AuditLogEndpointTests
{
    private sealed record Page(
        [property: JsonPropertyName("items")] List<Item> Items,
        [property: JsonPropertyName("totalCount")] int TotalCount);

    private sealed record Item(
        [property: JsonPropertyName("entityType")] string EntityType,
        [property: JsonPropertyName("property")] string Property,
        [property: JsonPropertyName("reason")] string? Reason);

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        var response = await factory.CreateClient().GetAsync("/api/v1/audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AsAdmin_ReturnsOnlyTheOwnDealersEntries_FilteredByEntity()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        Guid dealerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Database.ApplicationDbContext>();
            dealerId = (await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == "admin@carstore.com")).DealerId;
            DateTime at = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
            db.DealerAuditLogs.AddRange(
                DealerAuditLogEntry.Record(dealerId, "Car", Guid.NewGuid(), "Price", "1 ARS", "2 ARS", Guid.NewGuid(), "ajuste", "c1", at),
                DealerAuditLogEntry.Record(dealerId, "Sale", Guid.NewGuid(), "Status", "Completed", "Voided", Guid.NewGuid(), "Sale.Void: x", "c2", at),
                DealerAuditLogEntry.Record(Guid.NewGuid(), "Car", Guid.NewGuid(), "Price", "9 ARS", "8 ARS", Guid.NewGuid(), "ajeno", "c3", at));
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, await IntegrationTestHelpers.GetAdminTokenAsync(factory));

        var response = await client.GetAsync("/api/v1/audit-log?entity=Car&from=2026-09-01T00:00:00Z&to=2026-11-01T00:00:00Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Page>(IntegrationTestHelpers.JsonOptions);
        body!.Items.Should().ContainSingle().Which.Reason.Should().Be("ajuste");
        body.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Get_WithInvertedRange_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, await IntegrationTestHelpers.GetAdminTokenAsync(factory));

        var response = await client.GetAsync("/api/v1/audit-log?from=2026-11-01T00:00:00Z&to=2026-09-01T00:00:00Z");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
