using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Domain.Tenancy;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Tenancy;

/// <summary>RN-014 (A7): requesting an export and downloading it through an authenticated redirect.</summary>
public class TenantExportDownloadTests
{
    private static readonly Guid AdminDealer = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client)> CreateHostAsync(bool authenticated = true)
    {
        var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (authenticated)
        {
            IntegrationTestHelpers.SetAuthToken(client, await IntegrationTestHelpers.GetAdminTokenAsync(factory));
        }

        return (factory, client);
    }

    private static async Task<TenantExport> SeedReadyAsync(
        CustomWebApplicationFactory factory, Guid? dealerId = null, bool expired = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var export = TenantExport.Request(dealerId ?? AdminDealer, Guid.NewGuid(), now);
        export.MarkRunning(now);
        export.MarkReady($"exports/{export.DealerId}/{export.Id}.zip", new string('b', 64), 42, now);
        if (expired)
        {
            export.Expire(now);
        }

        db.TenantExports.Add(export);
        await db.SaveChangesAsync();
        return export;
    }

    [Fact]
    public async Task Download_ReadyExport_RedirectsToFifteenMinutePresignedUrl()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;
        var export = await SeedReadyAsync(factory);

        var response = await client.GetAsync($"/api/v1/dealer/exports/{export.Id}/download");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().Contain(export.ObjectKey!);
        location.Should().Contain("X-Amz-Expires=900");
    }

    [Fact]
    public async Task Download_WithoutToken_Returns401()
    {
        var (factory, client) = await CreateHostAsync(authenticated: false);
        await using var _ = factory;
        var export = await SeedReadyAsync(factory);

        var response = await client.GetAsync($"/api/v1/dealer/exports/{export.Id}/download");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Download_ExpiredExport_Returns404()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;
        var export = await SeedReadyAsync(factory, expired: true);

        var response = await client.GetAsync($"/api/v1/dealer/exports/{export.Id}/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_ExportOfAnotherDealer_Returns404()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;
        var export = await SeedReadyAsync(factory, dealerId: Guid.NewGuid());

        var response = await client.GetAsync($"/api/v1/dealer/exports/{export.Id}/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_UnknownExport_Returns404()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;

        var response = await client.GetAsync($"/api/v1/dealer/exports/{Guid.NewGuid()}/download");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RequestExport_ReturnsAcceptedWithId_AndASecondRequestConflicts()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;

        var first = await client.PostAsync("/api/v1/dealer/exports", content: null);
        var second = await client.PostAsync("/api/v1/dealer/exports", content: null);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RequestExport_WithoutToken_Returns401()
    {
        var (factory, client) = await CreateHostAsync(authenticated: false);
        await using var _ = factory;

        var response = await client.PostAsync("/api/v1/dealer/exports", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
