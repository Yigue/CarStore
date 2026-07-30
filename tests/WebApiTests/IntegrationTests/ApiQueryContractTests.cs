using FluentAssertions;
using System.Net;
using WebApiTests;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests;

public class ApiQueryContractTests
{
    [Theory]
    [InlineData("/api/v1/clients/search")]
    [InlineData("/api/v1/appointments")]
    public async Task SemanticallyRequiredParameters_Return400_WhenMissing(string endpoint)
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync(endpoint);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/clients/recent")]
    [InlineData("/api/v1/clients/top")]
    public async Task PaginationParameters_DefaultSafely_WhenMissing(string endpoint)
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync(endpoint);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
