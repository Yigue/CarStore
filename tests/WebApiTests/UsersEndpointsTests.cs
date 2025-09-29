using Application.Users.GetById;
using Infrastructure.Database;
using System.Net.Http.Headers;

namespace WebApiTests;

public class UsersEndpointsTests
{
    [Fact]
    public async Task RegisterUser_CreatesUser()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new { Email = "user@example.com", FirstName = "User", LastName = "Test", Password = "Password1!" };
        var response = await client.PostAsJsonAsync("/users/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Should().ContainSingle(u => u.Id == id);
    }

    [Fact]
    public async Task RegisterUser_ReturnsConflict_WhenEmailExists()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new { Email = "dup@example.com", FirstName = "User", LastName = "Test", Password = "Password1!" };
        await client.PostAsJsonAsync("/users/register", request);
        var response = await client.PostAsJsonAsync("/users/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetUserById_ReturnsUnauthorized_WithoutToken()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var register = new { Email = "noauth@example.com", FirstName = "No", LastName = "Auth", Password = "Password1!" };
        var regResponse = await client.PostAsJsonAsync("/users/register", register);
        var userId = await regResponse.Content.ReadFromJsonAsync<Guid>();
        var response = await client.GetAsync($"/users/{userId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_ReturnsUser_WhenAuthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var register = new { Email = "auth@example.com", FirstName = "Auth", LastName = "User", Password = "Password1!" };
        var regResponse = await client.PostAsJsonAsync("/users/register", register);
        var userId = await regResponse.Content.ReadFromJsonAsync<Guid>();

        var login = new { Email = "auth@example.com", Password = "Password1!" };
        var loginResponse = await client.PostAsJsonAsync("/users/login", login);
        var token = await loginResponse.Content.ReadFromJsonAsync<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/users/{userId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UserResponse>();
        result!.Id.Should().Be(userId);
        result.Email.Should().Be("auth@example.com");
    }
}
