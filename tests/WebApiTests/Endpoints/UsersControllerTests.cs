using Application.Abstractions.Authentication;
using Infrastructure.Database;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace WebApiTests.Endpoints;

public class UsersControllerTests
{
    [Fact]
    public async Task GetUsers_ReturnsUnauthorized_WithoutToken()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsers_ReturnsUserList_WhenAuthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Register and login a user
        var register = new
        {
            Email = "listuser@example.com",
            FirstName = "List",
            LastName = "User",
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var regResponse = await client.PostAsJsonAsync("/api/v1/users/register", register);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var regResult = await regResponse.Content.ReadFromJsonAsync<RegisterResponse>(IntegrationTestHelpers.JsonOptions);

        // Assign permission manually
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserPermissions.Add(new Domain.Users.UserPermission(regResult!.id, "CanManageUsers"));
        await context.SaveChangesAsync();

        // Login
        var login = new { Email = "listuser@example.com", Password = "Password1!" };
        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", login);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.token);

        // Get users
        var response = await client.GetAsync("/api/v1/users?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UserListResponse>(IntegrationTestHelpers.JsonOptions);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserById_ReturnsUser_WhenAuthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Register a user
        var register = new
        {
            Email = "getbyid@example.com",
            FirstName = "GetById",
            LastName = "Test",
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var regResponse = await client.PostAsJsonAsync("/api/v1/users/register", register);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var regResult = await regResponse.Content.ReadFromJsonAsync<RegisterResponse>(IntegrationTestHelpers.JsonOptions);

        // Assign permission
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserPermissions.Add(new Domain.Users.UserPermission(regResult!.id, "CanManageUsers"));
        await context.SaveChangesAsync();

        // Login
        var login = new { Email = "getbyid@example.com", Password = "Password1!" };
        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", login);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.token);

        // Get user by id
        var response = await client.GetAsync($"/api/v1/users/{regResult.id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UserDetailResponse>(IntegrationTestHelpers.JsonOptions);
        result!.Id.Should().Be(regResult.id);
        result.Email.Should().Be("getbyid@example.com");
    }

    [Fact]
    public async Task UpdateUser_ReturnsSuccess_WhenAuthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Register admin user
        var register = new
        {
            Email = "updateuser@example.com",
            FirstName = "Update",
            LastName = "User",
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var regResponse = await client.PostAsJsonAsync("/api/v1/users/register", register);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var regResult = await regResponse.Content.ReadFromJsonAsync<RegisterResponse>(IntegrationTestHelpers.JsonOptions);

        // Assign permissions
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserPermissions.Add(new Domain.Users.UserPermission(regResult!.id, "CanManageUsers"));
        await context.SaveChangesAsync();

        // Login
        var login = new { Email = "updateuser@example.com", Password = "Password1!" };
        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", login);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.token);

        // Create a role to assign
        var adminRole = new Domain.Users.Role(Guid.Parse(CustomWebApplicationFactory.AdminDealerId), "Admin", "Admin");
        using (var scope2 = factory.Services.CreateScope())
        {
            var ctx = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.Roles.Add(adminRole);
            await ctx.SaveChangesAsync();
        }

        // Update user
        var update = new
        {
            UserId = regResult.id,
            FirstName = "Updated",
            LastName = "Name",
            Phone = "+5491112345678",
            Role = adminRole.Id.ToString(),
            IsActive = true
        };
        var response = await client.PutAsJsonAsync($"/api/v1/users/{regResult.id}", update);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
    }

    [Fact]
    public async Task DeleteUser_ReturnsSuccess_WhenAuthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Register admin user (to delete another user)
        var adminRegister = new
        {
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User",
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var adminRegResponse = await client.PostAsJsonAsync("/api/v1/users/register", adminRegister);
        var adminResult = await adminRegResponse.Content.ReadFromJsonAsync<RegisterResponse>(IntegrationTestHelpers.JsonOptions);

        // Register a user to delete
        var deleteRegister = new
        {
            Email = "todelete@example.com",
            FirstName = "To",
            LastName = "Delete",
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var deleteRegResponse = await client.PostAsJsonAsync("/api/v1/users/register", deleteRegister);
        var deleteResult = await deleteRegResponse.Content.ReadFromJsonAsync<RegisterResponse>(IntegrationTestHelpers.JsonOptions);

        // Assign permissions to admin
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserPermissions.Add(new Domain.Users.UserPermission(adminResult!.id, "CanManageUsers"));
        await context.SaveChangesAsync();

        // Login as admin
        var login = new { Email = "admin@example.com", Password = "Password1!" };
        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", login);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.token);

        // Delete user
        var response = await client.DeleteAsync($"/api/v1/users/{deleteResult!.id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignRole_ReturnsSuccess_WhenAuthorized()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        // Register admin
        var adminRegister = new
        {
            Email = "assignrole@example.com",
            FirstName = "Assign",
            LastName = "Role",
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var adminRegResponse = await client.PostAsJsonAsync("/api/v1/users/register", adminRegister);
        var adminResult = await adminRegResponse.Content.ReadFromJsonAsync<RegisterResponse>(IntegrationTestHelpers.JsonOptions);

        // Register a user to update
        var targetRegister = new
        {
            Email = "target@example.com",
            FirstName = "Target",
            LastName = "User",
            Password = "Password1!",
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var targetRegResponse = await client.PostAsJsonAsync("/api/v1/users/register", targetRegister);
        var targetResult = await targetRegResponse.Content.ReadFromJsonAsync<RegisterResponse>(IntegrationTestHelpers.JsonOptions);

        // Assign permissions to admin
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.UserPermissions.Add(new Domain.Users.UserPermission(adminResult!.id, "CanManageRoles"));
        await context.SaveChangesAsync();

        // Login as admin
        var login = new { Email = "assignrole@example.com", Password = "Password1!" };
        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", login);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.token);

        // Create a role to assign
        var adminRole = new Domain.Users.Role(Guid.Parse(CustomWebApplicationFactory.AdminDealerId), "Admin", "Admin");
        using (var scope2 = factory.Services.CreateScope())
        {
            var ctx = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ctx.Roles.Add(adminRole);
            await ctx.SaveChangesAsync();
        }

        // Assign role
        var assignRole = new { Role = adminRole.Id.ToString() };
        var response = await client.PostAsJsonAsync($"/api/v1/users/{targetResult!.id}/role", assignRole);
        
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
    }

    private sealed record RegisterResponse(Guid id);
    private sealed record LoginResponse(string token);
    private sealed record UserListResponse(List<UserDetailResponse> users);
    private sealed record UserDetailResponse(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        string? Phone,
        string Role,
        bool IsActive,
        DateTime CreatedAt);
}