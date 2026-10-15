using System.Linq;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiTests.IntegrationTests.Newsletter;

/// <summary>
/// qa-p1-integridad D6 first fixed the positional-argument bug that put the email in the DNI slot. sfc B5.1 / RN-019
/// then stopped creating a placeholder <c>Client</c> altogether: a sign-up is a pending
/// <see cref="Domain.Marketing.Subscriber"/> with the email in its own field, awaiting double opt-in.
/// </summary>
public class NewsletterSubscribeTests
{
    [Fact]
    public async Task ValidEmail_Returns200_AndPersistsAPendingSubscriber_NotAClient()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/newsletter/subscribe",
            new { email = "persona@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var subscriber = await db.Subscribers.IgnoreQueryFilters()
            .SingleOrDefaultAsync(s => s.EmailNormalized == "persona@example.com");

        subscriber.Should().NotBeNull("the subscribe endpoint must have created a subscriber");
        subscriber!.Status.Should().Be(Domain.Marketing.SubscriberStatus.Pending);
        (await db.Clients.IgnoreQueryFilters().AnyAsync(c => c.DNI.StartsWith("NL-")))
            .Should().BeFalse("a newsletter sign-up is not a client");
    }

    [Fact]
    public async Task UnknownUnsubscribeToken_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/newsletter/unsubscribe?t=bogus", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
