using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Application.Users.Commands.CreateUser;
using Application.UnitTests;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace ApplicationTests.Users;

/// <summary>
/// spr PR 0.2 / REV-SDD-03 / BE-USERS-24: CreateUser must reject the same IDOR as AssignRole — a
/// role from another dealer and a role that does not exist must look exactly the same: 404.
/// </summary>
public class CreateUserHandlerTests
{
    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly TestApplicationDbContext _context;
    private readonly Mock<ICurrentTenantService> _tenant = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private Role _ownRole = null!;

    public CreateUserHandlerTests()
    {
        _context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _dealerId);
        _tenant.SetupGet(t => t.DealerId).Returns(_dealerId);
        _tenant.SetupGet(t => t.HasTenant).Returns(true);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _ownRole = new Role(_dealerId, "Vendedor", "Vendedor");
        _context.Roles.Add(_ownRole);
        _context.SaveChanges();
    }

    /// <summary>sfc A3.1: these tests exercise pre-existing CreateUser behavior, not entitlements — always allow.</summary>
    private static IEntitlementService AlwaysAllowEntitlements()
    {
        var mock = new Mock<IEntitlementService>();
        mock.Setup(s => s.EnsureCanAddAsync(It.IsAny<Guid>(), It.IsAny<PlanResource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return mock.Object;
    }

    private CreateUserCommandHandler Handler() =>
        new(_context, _context, _hasher.Object, _tenant.Object, AlwaysAllowEntitlements());

    private static CreateUserCommand Command(Guid roleId) =>
        new($"nuevo-{Guid.NewGuid()}@autossur.com", "Passw0rd!", "Nu", "Evo", null, roleId);

    [Fact]
    public async Task CreateUser_WithARoleOfAnotherDealer_Is404_AndNoUserIsCreated()
    {
        var otherDealerRole = new Role(Guid.NewGuid(), "Admin", "Admin de otra concesionaria");
        _context.Roles.Add(otherDealerRole);
        await _context.SaveChangesAsync();

        Result<Guid> result = await Handler().Handle(Command(otherDealerRole.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RoleErrors.NotFound(otherDealerRole.Id));
        result.Error.Type.Should().Be(ErrorType.NotFound);
        (await _context.Users.AnyAsync(u => u.DealerId == _dealerId)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateUser_WithANonexistentRole_Is404_IdenticalToAForeignDealerRole()
    {
        var otherDealerRole = new Role(Guid.NewGuid(), "Admin", "Admin de otra concesionaria");
        _context.Roles.Add(otherDealerRole);
        await _context.SaveChangesAsync();
        var nonexistentRoleId = Guid.NewGuid();

        Result<Guid> foreignResult = await Handler().Handle(Command(otherDealerRole.Id), CancellationToken.None);
        Result<Guid> nonexistentResult = await Handler().Handle(Command(nonexistentRoleId), CancellationToken.None);

        foreignResult.IsFailure.Should().BeTrue();
        nonexistentResult.IsFailure.Should().BeTrue();
        foreignResult.Error.Should().Be(RoleErrors.NotFound(otherDealerRole.Id));
        nonexistentResult.Error.Should().Be(RoleErrors.NotFound(nonexistentRoleId));
        foreignResult.Error.Code.Should().Be(nonexistentResult.Error.Code);
        foreignResult.Error.Type.Should().Be(nonexistentResult.Error.Type);
        (await _context.Users.AnyAsync(u => u.DealerId == _dealerId)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateUser_WithARoleOfTheOwnDealer_Succeeds()
    {
        Result<Guid> result = await Handler().Handle(Command(_ownRole.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        User created = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == result.Value);
        created.RoleId.Should().Be(_ownRole.Id);
        created.DealerId.Should().Be(_dealerId);
    }
}
