using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Domain.Billing;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.IntegrationTests.Billing;

/// <summary>
/// spr PR 1.3 (SPR-06/07, B4, task 1.3.2): <c>POST /api/v1/subscriptions/checkout</c> must take its
/// DealerId exclusively from the authenticated token's claims. A caller must never be able to create
/// (or route the invoices of) a checkout session for a dealer other than its own, even by sending that
/// dealer's id in the request body.
/// </summary>
public class CheckoutEndpointAuthTests
{
    private const string DealerBEmail = "admin@dealerb.test";
    private const string DealerBPassword = "Admin123!";

    [Fact]
    public async Task NoAuthHeader_ReturnsUnauthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/subscriptions/checkout",
            new { dealerId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthenticatedDealerB_BodyDealerId_IsIgnored_SessionIsCreatedForDealerB()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        var dealerAId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        Guid dealerBId = await SeedDealerBAdminAsync(factory);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, DealerBEmail, DealerBPassword);
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Dealer B's admin attempts to create a checkout session carrying Dealer A's id in the body.
        var response = await client.PostAsJsonAsync(
            "/api/v1/subscriptions/checkout",
            new { dealerId = dealerAId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<CheckoutResponse>(IntegrationTestHelpers.JsonOptions);
        payload!.Url.Should().Contain(dealerBId.ToString(),
            "the session must be created for the authenticated dealer (B), never for the body's dealerId (A)");
        payload.Url.Should().NotContain(dealerAId.ToString());
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/users/login",
            new { Email = email, Password = password },
            IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        return login!.Token;
    }

    private static async Task<Guid> SeedDealerBAdminAsync(CustomWebApplicationFactory factory)
    {
        var dealerBId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var dealerSettings = new Domain.DealerSettings.DealerSettings(dealerBId, "Dealer B", "contact@dealerb.test");
        context.DealerSettings.Add(dealerSettings);

        var role = new Role(dealerBId, "Admin", "Administrator");
        role.AddPermission("CanManageSettings");
        context.Roles.Add(role);

        var user = new User(dealerBId, DealerBEmail, "Dealer B", "Admin", passwordHasher.Hash(DealerBPassword), role.Id);
        context.Users.Add(user);

        await context.SaveChangesAsync();
        return dealerBId;
    }

    private sealed record LoginResponse(string Token);
    private sealed record CheckoutResponse(string Url);
}
