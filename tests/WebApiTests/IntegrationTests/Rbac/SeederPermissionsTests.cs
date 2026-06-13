using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiTests.IntegrationTests.Rbac;

/// <summary>
/// Verifies the seeding baseline (spec: rbac) — the admin gains CanManageSettings and a
/// default Empleado user is seeded with a read-focused permission set.
/// </summary>
public class SeederPermissionsTests
{
    private static async Task<string[]> PermissionsForAsync(CustomWebApplicationFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstAsync(u => u.Email == email);

        return await context.UserPermissions
            .IgnoreQueryFilters()
            .Where(p => p.UserId == user.Id)
            .Select(p => p.Permission)
            .ToArrayAsync();
    }

    [Fact]
    public async Task AdminSeed_IncludesCanManageSettings()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        var permissions = await PermissionsForAsync(factory, "admin@carstore.com");

        permissions.Should().Contain("CanManageSettings");
    }

    [Fact]
    public async Task EmpleadoSeed_HasDefaultReadPermissions()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        var permissions = await PermissionsForAsync(factory, "empleado@carstore.com");

        permissions.Should().Contain(new[]
        {
            "cars:read", "clients:read", "sales:read", "quotes:read",
            "leads:read", "appointments:read", "quotes:create"
        });
    }
}
