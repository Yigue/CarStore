using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Application.Users.Commands.AssignRole;
using Application.UnitTests;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace ApplicationTests.Users;

/// <summary>
/// spr PR 0.2 / REV-SDD-03 / BE-USERS-24: AssignRole is an IDOR waiting to happen if `RoleId` is
/// trusted blindly. A role from another dealer and a role that does not exist at all must produce
/// the exact same 404 — anything else leaks whether the id belongs to a foreign tenant.
/// </summary>
public class AssignRoleHandlerTests
{
    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly TestApplicationDbContext _context;
    private readonly Mock<ICurrentTenantService> _tenant = new();
    private Role _ownRole = null!;
    private User _user = null!;

    public AssignRoleHandlerTests()
    {
        _context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _dealerId);
        _tenant.SetupGet(t => t.DealerId).Returns(_dealerId);
        _tenant.SetupGet(t => t.HasTenant).Returns(true);
        _ownRole = new Role(_dealerId, "Vendedor", "Vendedor");
        _user = new User(_dealerId, "empleado@autossur.com", "Em", "Pleado", "hash", _ownRole.Id);
        _context.Roles.Add(_ownRole);
        _context.Users.Add(_user);
        _context.SaveChanges();
    }

    /// <summary>sfc A3.1: these tests exercise pre-existing IDOR behavior, not entitlements.</summary>
    private static IEntitlementService AlwaysAllowEntitlements()
    {
        var mock = new Mock<IEntitlementService>();
        mock.Setup(s => s.EnsureCanAddAsync(It.IsAny<Guid>(), It.IsAny<PlanResource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        mock.Setup(s => s.RoleConsumesASeatAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return mock.Object;
    }

    private AssignRoleCommandHandler Handler() => new(_context, _context, _tenant.Object, AlwaysAllowEntitlements());

    [Fact]
    public async Task AssignRole_WithARoleOfAnotherDealer_Is404()
    {
        var otherDealerRole = new Role(Guid.NewGuid(), "Admin", "Admin de otra concesionaria");
        _context.Roles.Add(otherDealerRole);
        await _context.SaveChangesAsync();

        Result<Guid> result = await Handler().Handle(new AssignRoleCommand(_user.Id, otherDealerRole.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RoleErrors.NotFound(otherDealerRole.Id));
        result.Error.Type.Should().Be(ErrorType.NotFound);
        User unchanged = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _user.Id);
        unchanged.RoleId.Should().Be(_ownRole.Id);
    }

    [Fact]
    public async Task AssignRole_WithANonexistentRole_Is404_IdenticalToAForeignDealerRole()
    {
        var otherDealerRole = new Role(Guid.NewGuid(), "Admin", "Admin de otra concesionaria");
        _context.Roles.Add(otherDealerRole);
        await _context.SaveChangesAsync();
        var nonexistentRoleId = Guid.NewGuid();

        Result<Guid> foreignResult = await Handler().Handle(new AssignRoleCommand(_user.Id, otherDealerRole.Id), CancellationToken.None);
        Result<Guid> nonexistentResult = await Handler().Handle(new AssignRoleCommand(_user.Id, nonexistentRoleId), CancellationToken.None);

        foreignResult.IsFailure.Should().BeTrue();
        nonexistentResult.IsFailure.Should().BeTrue();
        foreignResult.Error.Should().Be(RoleErrors.NotFound(otherDealerRole.Id));
        nonexistentResult.Error.Should().Be(RoleErrors.NotFound(nonexistentRoleId));
        // Same error shape (code/type) regardless of which case produced it — the message only
        // varies by the RoleId that was echoed back, never by WHY it failed.
        foreignResult.Error.Code.Should().Be(nonexistentResult.Error.Code);
        foreignResult.Error.Type.Should().Be(nonexistentResult.Error.Type);
    }

    [Fact]
    public async Task AssignRole_WithARoleOfTheOwnDealer_Succeeds()
    {
        var anotherOwnRole = new Role(_dealerId, "Admin", "Admin");
        _context.Roles.Add(anotherOwnRole);
        await _context.SaveChangesAsync();

        Result<Guid> result = await Handler().Handle(new AssignRoleCommand(_user.Id, anotherOwnRole.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        User updated = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _user.Id);
        updated.RoleId.Should().Be(anotherOwnRole.Id);
    }
}
