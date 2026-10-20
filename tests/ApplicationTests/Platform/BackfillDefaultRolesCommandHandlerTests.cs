using Application.Platform.Backfill.BackfillDefaultRoles;
using Domain.Cars;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Platform;

/// <summary>
/// RED tests for <see cref="BackfillDefaultRolesCommandHandler"/> — spr PR 4.1 (SPR-18, D6).
/// </summary>
public class BackfillDefaultRolesCommandHandlerTests
{
    private static readonly Guid ActorUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Guid SeedDealer(TestApplicationDbContext context)
    {
        var dealerId = Guid.NewGuid();
        var dealer = new Domain.DealerSettings.DealerSettings(dealerId, "Test Dealer", "test@dealer.com");
        context.DealerSettings.Add(dealer);
        context.SaveChanges();
        dealer.ClearDomainEvents();
        return dealerId;
    }

    [Fact]
    public async Task Handle_DryRun_OnDealerMissingAllDefaultRoles_ReportsMissingRoles_WritesNothing()
    {
        // Arrange
        using var context = CreateContext();
        Guid dealerId = SeedDealer(context);
        var handler = new BackfillDefaultRolesCommandHandler(context, new FakeDateTimeProvider(), new RecordingPermissionCacheInvalidator());

        // Act
        var result = await handler.Handle(new BackfillDefaultRolesCommand(DryRun: true, ActorUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Action.Should().Be(BackfillAction.DryRun);
        result.Value.DealersAffected.Should().Be(1);

        DealerRoleDiff diff = result.Value.Diffs.Single();
        diff.DealerId.Should().Be(dealerId);
        diff.MissingRoles.Should().BeEquivalentTo([DefaultRoleCatalog.Admin, DefaultRoleCatalog.Empleado, DefaultRoleCatalog.Cliente]);

        // Dry run must never write roles/permissions.
        context.Roles.IgnoreQueryFilters().Any().Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Confirmed_OnDealerMissingAllDefaultRoles_CreatesRolesWithCatalogPermissions()
    {
        // Arrange
        using var context = CreateContext();
        Guid dealerId = SeedDealer(context);
        var handler = new BackfillDefaultRolesCommandHandler(context, new FakeDateTimeProvider(), new RecordingPermissionCacheInvalidator());

        // Act
        var result = await handler.Handle(new BackfillDefaultRolesCommand(DryRun: false, ActorUserId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Action.Should().Be(BackfillAction.Apply);
        result.Value.DealersAffected.Should().Be(1);

        List<Role> roles = await context.Roles.IgnoreQueryFilters().Include(r => r.Permissions)
            .Where(r => r.DealerId == dealerId).ToListAsync();
        roles.Select(r => r.Name).Should().BeEquivalentTo([DefaultRoleCatalog.Admin, DefaultRoleCatalog.Empleado, DefaultRoleCatalog.Cliente]);

        Role adminRole = roles.Single(r => r.Name == DefaultRoleCatalog.Admin);
        adminRole.Permissions.Select(p => p.Permission)
            .Should().BeEquivalentTo(DefaultRoleCatalog.GetPermissions(DefaultRoleCatalog.Admin));

        List<BackfillAudit> audits = context.Set<BackfillAudit>().IgnoreQueryFilters().ToList();
        audits.Should().ContainSingle(a => a.DealerId == dealerId && a.Action == BackfillAction.Apply);
    }

    // Security review (spr PR 4.1): "extra" cannot be told apart from a dealer's own customization of its Admin
    // role through the normal role-editing screen, so an unattended, platform-wide run must only ever add what a
    // role is missing — never remove anything, even something that looks like code drift.
    [Fact]
    public async Task Handle_Confirmed_OnRoleWithDriftedPermissions_AddsWhatIsMissing_ButKeepsTheExtraGrant()
    {
        // Arrange — Admin role missing a catalog permission and holding an extra one.
        using var context = CreateContext();
        Guid dealerId = SeedDealer(context);
        Role driftedAdmin = DefaultRoleCatalog.BuildRole(dealerId, DefaultRoleCatalog.Admin);
        driftedAdmin.RemovePermission("cars:delete");
        driftedAdmin.AddPermission("some:legacy:grant");
        context.Roles.Add(driftedAdmin);
        context.Roles.Add(DefaultRoleCatalog.BuildRole(dealerId, DefaultRoleCatalog.Empleado));
        context.Roles.Add(DefaultRoleCatalog.BuildRole(dealerId, DefaultRoleCatalog.Cliente));
        context.SaveChanges();

        var invalidator = new RecordingPermissionCacheInvalidator();
        var handler = new BackfillDefaultRolesCommandHandler(context, new FakeDateTimeProvider(), invalidator);

        // Act
        var result = await handler.Handle(new BackfillDefaultRolesCommand(DryRun: false, ActorUserId), CancellationToken.None);

        // Assert
        result.Value.DealersAffected.Should().Be(1);

        Role syncedAdmin = await context.Roles.IgnoreQueryFilters().Include(r => r.Permissions)
            .SingleAsync(r => r.Id == driftedAdmin.Id);
        var expected = DefaultRoleCatalog.GetPermissions(DefaultRoleCatalog.Admin).Append("some:legacy:grant");
        syncedAdmin.Permissions.Select(p => p.Permission).Should().BeEquivalentTo(expected,
            "the missing grant is added, but the extra one stays — it may be a deliberate dealer customization");
        invalidator.InvalidatedRoleIds.Should().Contain(driftedAdmin.Id);
    }

    private sealed class RecordingPermissionCacheInvalidator : Application.Abstractions.Authorization.IPermissionCacheInvalidator
    {
        public List<Guid> InvalidatedRoleIds { get; } = [];
        public List<Guid> InvalidatedUserIds { get; } = [];

        public Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            InvalidatedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            InvalidatedRoleIds.Add(roleId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_ConfirmedTwice_IsIdempotent_SecondRunReportsZeroChanges()
    {
        // Arrange
        using var context = CreateContext();
        SeedDealer(context);
        var handler = new BackfillDefaultRolesCommandHandler(context, new FakeDateTimeProvider(), new RecordingPermissionCacheInvalidator());

        // Act — first confirmed run converges the dealer.
        var first = await handler.Handle(new BackfillDefaultRolesCommand(DryRun: false, ActorUserId), CancellationToken.None);
        first.Value.DealersAffected.Should().Be(1);

        // Act — second confirmed run on the now-converged dealer.
        var second = await handler.Handle(new BackfillDefaultRolesCommand(DryRun: false, ActorUserId), CancellationToken.None);

        // Assert
        second.IsSuccess.Should().BeTrue();
        second.Value.DealersAffected.Should().Be(0);
        second.Value.Diffs.Should().BeEmpty();

        // No new audit rows for the no-op second run.
        List<BackfillAudit> audits = context.Set<BackfillAudit>().IgnoreQueryFilters().ToList();
        audits.Should().HaveCount(1);
    }
}
