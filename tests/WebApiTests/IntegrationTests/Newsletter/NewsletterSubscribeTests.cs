using System.Linq;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiTests.IntegrationTests.Newsletter;

/// <summary>
/// qa-p1-integridad PR5, Slice 10 (D6, REQ: crm-client-data-contract). <c>Subscribe.cs</c>
/// constructed <see cref="Application.Clients.Create.CreateClientCommand"/> positionally against
/// a record shaped <c>(FirstName, LastName, DNI, Email, Phone, Address, Type, ...)</c>. The
/// subscriber's address landed in the <c>DNI</c> slot and the literal <c>"N/A"</c> landed in
/// <c>Email</c>, so <c>new Email("N/A")</c> always threw a <see cref="SharedKernel.DomainException"/>
/// and the endpoint returned 400 for every request.
/// </summary>
public class NewsletterSubscribeTests
{
    [Fact]
    public async Task ValidEmail_Returns200_AndPersistsEmailInEmailField_NotDni()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/newsletter/subscribe",
            new { email = "persona@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a well-formed email must not be rejected — the positional-argument bug made every " +
            "subscription throw a DomainException from new Email(\"N/A\")");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Clients
            .IgnoreQueryFilters()
            .Where(c => c.DNI.StartsWith("NL-"))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        persisted.Should().NotBeNull("the subscribe endpoint must have created a client");
        persisted!.Email.Value.Should().Be("persona@example.com",
            "the request's email must land in the Email field, not the DNI field");
        persisted.DNI.Should().StartWith("NL-",
            "DNI must hold the generated newsletter placeholder, not the subscriber's email");
        persisted.DNI.Should().NotBe("persona@example.com");
    }
}
