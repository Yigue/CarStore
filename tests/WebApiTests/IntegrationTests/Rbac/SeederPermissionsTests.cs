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

        return await context.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => rp.RoleId == user.RoleId)
            .Select(rp => rp.Permission)
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
            "cars:read", "clients:read", "sales:read",
            "leads:read", "appointments:read"
        });
    }

    [Fact]
    public async Task EmpleadoSeed_DoesNotIncludeQuotesPermissions()
    {
        // REQ-QT-RBAC-001: Cotizaciones is Admin-only by default (Etapa 3) — the
        // Empleado default seed no longer grants quotes:read/quotes:create.
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        var permissions = await PermissionsForAsync(factory, "empleado@carstore.com");

        permissions.Should().NotContain(new[] { "quotes:read", "quotes:create" });
    }

    [Fact]
    public async Task EmpleadoSeed_CanCreateAndUpdateSales_But_NotDelete()
    {
        // Bug 4: Empleado must be able to perform sales CRUD (create/update) it was
        // previously missing, while deletion stays admin-only.
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        var permissions = await PermissionsForAsync(factory, "empleado@carstore.com");

        permissions.Should().Contain(new[] { "sales:create", "sales:update" });
        permissions.Should().NotContain("sales:delete");
    }

    [Fact]
    public async Task EmpleadoSeed_ReconcilesMissingPermissions_OnExistingUser()
    {
        // The Empleado seeder used to be all-or-nothing: if the user already had ANY
        // permission row, the whole block was skipped, so newly added permissions
        // (e.g. sales:create/sales:update) never reached databases seeded before
        // those permissions existed. This proves the reconcile fix: an existing
        // empleado with a partial permission set gains the missing ones on re-seed,
        // without losing the ones it already had.
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var empleado = await context.Users
                .IgnoreQueryFilters()
                .FirstAsync(u => u.Email == "empleado@carstore.com");

            // Simulate a database seeded before sales:create/sales:update existed by
            // stripping them (and a couple of other permissions) down to a partial set.
            var toRemove = await context.RolePermissions
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == empleado.RoleId
                    && (rp.Permission == "sales:create" || rp.Permission == "sales:update" || rp.Permission == "leads:read"))
                .ToListAsync();

            context.RolePermissions.RemoveRange(toRemove);
            await context.SaveChangesAsync();
        }

        var partialPermissions = await PermissionsForAsync(factory, "empleado@carstore.com");
        partialPermissions.Should().NotContain(new[] { "sales:create", "sales:update", "leads:read" });

        // Re-seed: reconcile must add back only the missing permissions.
        factory.SeedDatabase();

        var reconciledPermissions = await PermissionsForAsync(factory, "empleado@carstore.com");

        reconciledPermissions.Should().Contain(new[]
        {
            "cars:read", "clients:read", "sales:read", "sales:create", "sales:update",
            "leads:read", "appointments:read"
        });

        // No duplicates were inserted for permissions that were never removed.
        reconciledPermissions.Should().OnlyHaveUniqueItems();
    }
}
