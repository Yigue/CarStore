using Domain.Users;

namespace ApplicationTests.Users;

/// <summary>
/// RED tests for <see cref="DefaultRoleCatalog"/> — spr PR 4.1 (SPR-18, D6).
/// </summary>
public class DefaultRoleCatalogTests
{
    [Theory]
    [InlineData(DefaultRoleCatalog.Admin)]
    [InlineData(DefaultRoleCatalog.Empleado)]
    [InlineData(DefaultRoleCatalog.Cliente)]
    public void Sync_OnAnAlreadySeededDealer_YieldsSamePermissionSet_AsSeedingABrandNewOne(string roleName)
    {
        // Arrange — a brand-new dealer seeded straight from the catalogue.
        var freshDealerId = Guid.NewGuid();
        Role freshRole = DefaultRoleCatalog.BuildRole(freshDealerId, roleName);

        // An existing dealer whose role has drifted from the catalogue: an extra grant it
        // should not have, and missing a permission the catalogue says it should have.
        var existingDealerId = Guid.NewGuid();
        Role existingRole = DefaultRoleCatalog.BuildRole(existingDealerId, roleName);
        existingRole.AddPermission("some:drifted:permission");
        foreach (RolePermission permission in existingRole.Permissions.ToList())
        {
            existingRole.RemovePermission(permission.Permission);
            break;
        }

        // Act — re-seed (sync) the existing dealer's role.
        DefaultRoleCatalog.Sync(existingRole);

        // Assert — re-seeding converges to exactly the same permission set a fresh seed gets.
        existingRole.Permissions.Select(p => p.Permission)
            .Should().BeEquivalentTo(freshRole.Permissions.Select(p => p.Permission));
    }

    [Fact]
    public void Sync_CalledTwice_IsIdempotent()
    {
        var dealerId = Guid.NewGuid();
        Role role = DefaultRoleCatalog.BuildRole(dealerId, DefaultRoleCatalog.Admin);
        role.AddPermission("legacy:leftover");

        DefaultRoleCatalog.Sync(role);
        var afterFirstSync = role.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList();

        DefaultRoleCatalog.Sync(role);
        var afterSecondSync = role.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList();

        afterSecondSync.Should().BeEquivalentTo(afterFirstSync);
    }

    [Fact]
    public void Sync_OnACustomDealerRole_IsANoOp()
    {
        var dealerId = Guid.NewGuid();
        var customRole = new Role(dealerId, "Supervisor", "Rol a medida de la concesionaria");
        customRole.AddPermission("cars:read");

        DefaultRoleCatalog.Sync(customRole);

        customRole.Permissions.Select(p => p.Permission).Should().BeEquivalentTo(["cars:read"]);
    }

    [Fact]
    public void Diff_OnAFreshlyBuiltRole_ReportsNoDifferences()
    {
        var dealerId = Guid.NewGuid();
        Role role = DefaultRoleCatalog.BuildRole(dealerId, DefaultRoleCatalog.Empleado);

        (IReadOnlyList<string> missing, IReadOnlyList<string> extra) = DefaultRoleCatalog.Diff(role);

        missing.Should().BeEmpty();
        extra.Should().BeEmpty();
    }

    [Fact]
    public void GetPermissions_ForUnknownRoleName_Throws()
    {
        Action act = () => DefaultRoleCatalog.GetPermissions("NotARole");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
