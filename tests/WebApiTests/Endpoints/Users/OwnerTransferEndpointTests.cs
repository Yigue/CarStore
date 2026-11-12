using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Users.Verification;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Endpoints.Users;

/// <summary>sfc A4.3 / A22 / RN-029: owner transfer with double confirmation over HTTP.</summary>
public class OwnerTransferEndpointTests
{
    private static readonly Guid AdminDealer = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
    private const string TargetEmail = "nuevo.owner@carstore.com";
    private const string TargetPassword = "Target123!";
    private const string AdminPassword = "Admin123!";
    private const string Plain = "owner-transfer-plain-token-for-the-test";

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client)> CreateHostAsync()
    {
        var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, await IntegrationTestHelpers.GetAdminTokenAsync(factory));
        return (factory, client);
    }

    private static async Task<Guid> SeedAdminAsync(CustomWebApplicationFactory factory, bool verified = true, Guid? dealerId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Guid dealer = dealerId ?? AdminDealer;
        Role? role = await db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.DealerId == dealer && r.Name == "Admin");
        if (role is null)
        {
            role = new Role(dealer, "Admin", "Admin");
            db.Roles.Add(role);
        }

        var user = new User(dealer, dealerId is null ? TargetEmail : "foreign@otrodealer.com", "Nuevo", "Owner", hasher.Hash(TargetPassword), role.Id);
        if (verified)
            user.MarkEmailVerified(DateTime.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid?> OwnerAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.DealerSettings.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.DealerId == AdminDealer)).OwnerUserId;
    }

    private static async Task SeedTokenAsync(CustomWebApplicationFactory factory, Guid targetId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid? ownerId = (await db.DealerSettings.IgnoreQueryFilters().SingleAsync(d => d.DealerId == AdminDealer)).OwnerUserId;
        db.UserActionTokens.Add(UserActionToken.Issue(
            [], UserActionPurpose.OwnerTransfer, AdminDealer, TargetEmail, UserActionTokenHasher.Hash(Plain),
            targetId, null, ownerId, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    private static async Task<HttpClient> ClientFor(CustomWebApplicationFactory factory, string email, string password)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/users/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions))!.Token;
        IntegrationTestHelpers.SetAuthToken(client, token);
        return client;
    }

    private sealed record LoginResponse(string Token);

    [Fact]
    public async Task WithoutToken_BothEndpointsReturn401()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var initiate = await client.PostAsJsonAsync("/api/v1/dealer/owner-transfer", new { targetUserId = Guid.NewGuid(), password = AdminPassword });
        var accept = await client.PostAsJsonAsync("/api/v1/dealer/owner-transfer/accept", new { token = Plain });

        initiate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        accept.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Initiate_ByTheSeededOwner_StoresAPendingHashOnlyToken_AndDoesNotSwapYet()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;
        Guid targetId = await SeedAdminAsync(factory);
        Guid? ownerBefore = await OwnerAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/dealer/owner-transfer", new { targetUserId = targetId, password = AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserActionToken token = await db.UserActionTokens.SingleAsync(t => t.Purpose == UserActionPurpose.OwnerTransfer);
        token.UserId.Should().Be(targetId);
        token.TokenHash.Should().MatchRegex("^[0-9A-F]{64}$");
        (await OwnerAsync(factory)).Should().Be(ownerBefore);
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Initiate_WithAWrongOrMissingPassword_IsRejectedAndStoresNoToken(string? password)
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;
        Guid targetId = await SeedAdminAsync(factory);

        var response = await client.PostAsJsonAsync("/api/v1/dealer/owner-transfer", new { targetUserId = targetId, password });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.UserActionTokens.CountAsync(t => t.Purpose == UserActionPurpose.OwnerTransfer)).Should().Be(0);
    }

    [Fact]
    public async Task Initiate_ToAUserOfAnotherDealer_Returns404()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;
        Guid foreign = await SeedAdminAsync(factory, dealerId: Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/v1/dealer/owner-transfer", new { targetUserId = foreign, password = AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Initiate_ByAnAdminWhoIsNotTheOwner_Returns403()
    {
        var (factory, _) = await CreateHostAsync();
        await using var __ = factory;
        await SeedAdminAsync(factory);
        var other = await ClientFor(factory, TargetEmail, TargetPassword);

        var response = await other.PostAsJsonAsync("/api/v1/dealer/owner-transfer", new { targetUserId = Guid.NewGuid(), password = AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Accept_ByTheRecipient_SwapsTheOwner_AndTheTokenIsSingleUse()
    {
        var (factory, client) = await CreateHostAsync();
        await using var _ = factory;
        Guid targetId = await SeedAdminAsync(factory);
        await SeedTokenAsync(factory, targetId);
        var recipient = await ClientFor(factory, TargetEmail, TargetPassword);

        var first = await recipient.PostAsJsonAsync("/api/v1/dealer/owner-transfer/accept", new { token = Plain });
        var replay = await recipient.PostAsJsonAsync("/api/v1/dealer/owner-transfer/accept", new { token = Plain });

        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await OwnerAsync(factory)).Should().Be(targetId);

        // The former owner kept access and is no longer the owner: it cannot start another transfer.
        var former = await client.PostAsJsonAsync("/api/v1/dealer/owner-transfer", new { targetUserId = targetId, password = AdminPassword });
        former.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Accept_WithAnUnknownToken_Returns400()
    {
        var (factory, _) = await CreateHostAsync();
        await using var __ = factory;
        await SeedAdminAsync(factory);
        var recipient = await ClientFor(factory, TargetEmail, TargetPassword);

        var response = await recipient.PostAsJsonAsync("/api/v1/dealer/owner-transfer/accept", new { token = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
