using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Users.Verification;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Endpoints.Users;

/// <summary>sfc A4.1 / RN-030: the verification endpoints are real (H10) and do not leak which accounts exist.</summary>
public class VerifyEmailEndpointTests
{
    private const string Plain = "known-plain-token-for-the-test";

    private static async Task<Guid> SeedTokenAsync(CustomWebApplicationFactory factory, string email, DateTime issuedAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await db.Users.IgnoreQueryFilters().SingleAsync(u => (string)u.Email == email);
        db.UserActionTokens.Add(UserActionToken.Issue(
            [], UserActionPurpose.EmailVerification, user.DealerId, email,
            UserActionTokenHasher.Hash(Plain), user.Id, null, null, issuedAtUtc));
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<DateTime?> VerifiedAtAsync(CustomWebApplicationFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == userId)).EmailVerifiedAtUtc;
    }

    [Fact]
    public async Task VerifyEmail_RejectsAnUnknownToken_With400()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/users/verify-email", new { Token = "anything-at-all" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Users.InvalidVerificationToken");
    }

    [Fact]
    public async Task VerifyEmail_AcceptsAValidTokenOnce_AndMarksTheUserVerified()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();
        Guid userId = await SeedTokenAsync(factory, "admin@carstore.com", DateTime.UtcNow);

        var first = await client.PostAsJsonAsync("/api/v1/users/verify-email", new { Token = Plain });
        var replay = await client.PostAsJsonAsync("/api/v1/users/verify-email", new { Token = Plain });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await VerifiedAtAsync(factory, userId)).Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyEmail_RejectsAnExpiredToken_WithoutVerifyingTheUser()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();
        Guid userId = await SeedTokenAsync(factory, "admin@carstore.com", DateTime.UtcNow.AddHours(-25));

        var response = await client.PostAsJsonAsync("/api/v1/users/verify-email", new { Token = Plain });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await VerifiedAtAsync(factory, userId)).Should().BeNull();
    }

    [Fact]
    public async Task ResendVerification_AnswersIdentically_WhetherTheAccountExistsOrNot()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var known = await client.PostAsJsonAsync("/api/v1/users/resend-verification", new { Email = "admin@carstore.com" });
        var unknown = await client.PostAsJsonAsync("/api/v1/users/resend-verification", new { Email = "nobody@nowhere.test" });
        var malformed = await client.PostAsJsonAsync("/api/v1/users/resend-verification", new { Email = "not-an-email" });

        known.StatusCode.Should().Be(HttpStatusCode.OK);
        unknown.StatusCode.Should().Be(known.StatusCode);
        malformed.StatusCode.Should().Be(known.StatusCode);
        string body = await known.Content.ReadAsStringAsync();
        (await unknown.Content.ReadAsStringAsync()).Should().Be(body);
        (await malformed.Content.ReadAsStringAsync()).Should().Be(body);
        body.Should().NotContain("stub");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokens = await db.UserActionTokens.AsNoTracking().ToListAsync();
        tokens.Should().ContainSingle().Which.Email.Should().Be("admin@carstore.com");
    }
}
