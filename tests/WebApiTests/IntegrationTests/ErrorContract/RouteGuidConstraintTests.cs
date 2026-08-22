using System.Net;
using FluentAssertions;

namespace WebApiTests.IntegrationTests.ErrorContract;

/// <summary>
/// qa-p1-integridad PR1, Slice 1.5 (D1, REQ: api-error-contract "Non-GUID Path Segments Are
/// Rejected By Route Constraints"). Every endpoint with a <c>Guid</c> route segment must declare
/// <c>{id:guid}</c> — without it, a non-GUID segment matches the route, model binding then fails
/// to parse it as a <c>Guid</c>, and that failure throws (surfacing as 500 in Development before
/// this PR, or a bodiless 400 elsewhere). With the constraint, a non-GUID segment is simply a
/// routing miss: 404, cleanly, in every environment.
/// </summary>
public class RouteGuidConstraintTests
{
    [Fact]
    public async Task GetCarById_WithNonGuidSegment_Returns404NotBadRequestOrServerError()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/cars/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a non-GUID path segment must be a routing miss (404), never a bind failure (400/500)");
    }
}
