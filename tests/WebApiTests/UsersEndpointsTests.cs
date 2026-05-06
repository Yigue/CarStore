using Application.Users.GetById;
using Infrastructure.Database;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace WebApiTests;

public class UsersEndpointsTests
{
    [Fact]
    public async Task RegisterUser_CreatesUser()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new { 
            Email = "user@example.com", 
            FirstName = "User", 
            LastName = "Test", 
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var response = await client.PostAsJsonAsync("/api/v1/users/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        var id = result!.id;
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.IgnoreQueryFilters().Should().ContainSingle(u => u.Id == id);
    }

    [Fact]
    public async Task RegisterUser_ReturnsConflict_WhenEmailExists()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new { 
            Email = "dup@example.com", 
            FirstName = "User", 
            LastName = "Test", 
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        await client.PostAsJsonAsync("/api/v1/users/register", request);
        var response = await client.PostAsJsonAsync("/api/v1/users/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetUserById_ReturnsUnauthorized_WithoutToken()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var register = new { 
            Email = "noauth@example.com", 
            FirstName = "No", 
            LastName = "Auth", 
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var regResponse = await client.PostAsJsonAsync("/api/v1/users/register", register);
        var result = await regResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        var userId = result!.id;
        var response = await client.GetAsync($"/api/v1/users/{userId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_ReturnsUser_WhenAuthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var register = new { 
            Email = "auth@example.com", 
            FirstName = "Auth", 
            LastName = "User", 
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var regResponse = await client.PostAsJsonAsync("/api/v1/users/register", register);
        var regResult = await regResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        var userId = regResult!.id;

        // Asignar permiso manualmente para el test
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserPermissions.Add(new Domain.Users.UserPermission(userId, "users:access"));
        await context.SaveChangesAsync();

        var login = new { Email = "auth@example.com", Password = "Password1!" };
        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", login);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.token);     

        var response = await client.GetAsync($"/api/v1/users/{userId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UserResponse>();
        result!.Id.Should().Be(userId);
        result.Email.Should().Be("auth@example.com");
    }

    private sealed record RegisterResponse(Guid id);
    private sealed record LoginResponse(string token);
}
