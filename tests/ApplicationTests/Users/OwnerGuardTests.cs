using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Users.Commands.AssignRole;
using Application.Users.Commands.DeleteUser;
using Application.Users.Commands.UpdateUser;
using Application.UnitTests;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace ApplicationTests.Users;

/// <summary>
/// sfc A4.3 / A22 (design C17): the owner is never left without the account. It cannot be deactivated, deleted or
/// have its role changed from <c>AssignRole</c>, <c>UpdateUser</c> or <c>DeleteUser</c>; it must be transferred first.
/// </summary>
public class OwnerGuardTests
{
    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly TestApplicationDbContext _context;
    private readonly Mock<ICurrentTenantService> _tenant = new();
    private readonly Mock<IUserContext> _actor = new();
    private Role _admin = null!;
    private Role _seller = null!;
    private User _owner = null!;
    private User _colleague = null!;

    public OwnerGuardTests()
    {
        _context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _dealerId);
        _tenant.SetupGet(t => t.DealerId).Returns(_dealerId);
        _admin = new Role(_dealerId, "Admin", "Admin");
        _seller = new Role(_dealerId, "Vendedor", "Vendedor");
        _context.Roles.AddRange(_admin, _seller);
        _owner = new User(_dealerId, "owner@autossur.com", "Ana", "Owner", "hash", _admin.Id);
        _colleague = new User(_dealerId, "colega@autossur.com", "Beto", "Colega", "hash", _admin.Id);
        _context.Users.AddRange(_owner, _colleague);
        var dealer = new DealerSettingsEntity(_dealerId, _dealerId, "Autos Sur SA", "contacto@autossur.com");
        dealer.SetOwner(_owner.Id);
        _context.DealerSettings.Add(dealer);
        _context.SaveChanges();
        // The actor is another administrator: self-protection rules must not be what blocks the call.
        _actor.SetupGet(u => u.UserId).Returns(_colleague.Id);
    }

    private AssignRoleCommandHandler AssignRole() => new(_context, _tenant.Object);
    private UpdateUserCommandHandler UpdateUser() => new(_context, _tenant.Object);
    private DeleteUserCommandHandler DeleteUser() => new(_context, _tenant.Object, _actor.Object);

    private UpdateUserCommand Update(User user, Guid roleId, bool isActive) =>
        new(user.Id, "Nombre", "Apellido", null, roleId, isActive);

    [Fact]
    public async Task AssignRole_DemotingTheOwner_IsRejected_AndTheRoleIsKept()
    {
        Result<Guid> result = await AssignRole().Handle(new AssignRoleCommand(_owner.Id, _seller.Id), CancellationToken.None);

        result.Error.Should().Be(UserErrors.OwnerProtected);
        (await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _owner.Id)).RoleId.Should().Be(_admin.Id);
    }

    [Fact]
    public async Task AssignRole_ToAnotherUser_StillWorks()
    {
        Result<Guid> result = await AssignRole().Handle(new AssignRoleCommand(_colleague.Id, _seller.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _colleague.Id)).RoleId.Should().Be(_seller.Id);
    }

    [Fact]
    public async Task AssignRole_ReassigningTheOwnersCurrentRole_IsHarmless()
    {
        Result<Guid> result = await AssignRole().Handle(new AssignRoleCommand(_owner.Id, _admin.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUser_DeactivatingTheOwner_IsRejected()
    {
        Result<Guid> result = await UpdateUser().Handle(Update(_owner, _admin.Id, isActive: false), CancellationToken.None);

        result.Error.Should().Be(UserErrors.OwnerProtected);
        (await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _owner.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUser_ChangingTheOwnersRole_IsRejected()
    {
        Result<Guid> result = await UpdateUser().Handle(Update(_owner, _seller.Id, isActive: true), CancellationToken.None);

        result.Error.Should().Be(UserErrors.OwnerProtected);
        (await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _owner.Id)).RoleId.Should().Be(_admin.Id);
    }

    [Fact]
    public async Task UpdateUser_RenamingTheOwner_KeepingRoleAndState_IsAllowed()
    {
        Result<Guid> result = await UpdateUser().Handle(Update(_owner, _admin.Id, isActive: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _owner.Id)).FirstName.Should().Be("Nombre");
    }

    [Fact]
    public async Task UpdateUser_DeactivatingAnotherUser_StillWorks()
    {
        Result<Guid> result = await UpdateUser().Handle(Update(_colleague, _admin.Id, isActive: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _colleague.Id)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUser_TheOwner_IsRejected_AndStaysActive()
    {
        Result<Guid> result = await DeleteUser().Handle(new DeleteUserCommand(_owner.Id), CancellationToken.None);

        result.Error.Should().Be(UserErrors.OwnerProtected);
        (await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _owner.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUser_AnotherUser_StillWorks()
    {
        Result<Guid> result = await DeleteUser().Handle(new DeleteUserCommand(_colleague.Id), CancellationToken.None);

        // The actor in this fixture is the colleague himself: the self-delete rule applies, proving it still runs first.
        result.Error.Should().Be(UserErrors.SelfDeleteNotAllowed);

        _actor.SetupGet(u => u.UserId).Returns(_owner.Id);
        Result<Guid> byOwner = await DeleteUser().Handle(new DeleteUserCommand(_colleague.Id), CancellationToken.None);

        byOwner.IsSuccess.Should().BeTrue();
        (await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _colleague.Id)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DealerWithoutOwner_DoesNotBlockUserManagement()
    {
        // Legacy row not backfilled: there is no owner to protect (the destructive paths that need an owner fail
        // closed elsewhere), so the team can still be managed.
        var legacyId = Guid.NewGuid();
        _tenant.SetupGet(t => t.DealerId).Returns(legacyId);
        Role legacyAdmin = new(legacyId, "Admin", "Admin");
        _context.Roles.Add(legacyAdmin);
        _context.DealerSettings.Add(new DealerSettingsEntity(legacyId, legacyId, "Legacy SA", "legacy@x.com"));
        var legacyUser = new User(legacyId, "legacy@x.com", "Leo", "Legacy", "hash", legacyAdmin.Id);
        _context.Users.Add(legacyUser);
        await _context.SaveChangesAsync();

        Result<Guid> result = await UpdateUser().Handle(Update(legacyUser, legacyAdmin.Id, isActive: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
