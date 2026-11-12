using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Application.UnitTests;
using Application.Users.Commands.GrantDealerMembership;
using Application.Users.Login;
using Application.Users.SelectDealer;
using Application.Users.SwitchDealer;
using Domain.Users;
using Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace ApplicationTests.Users;

/// <summary>
/// sfc B6.3 / B22 / RN-043: a single-dealer user and SuperAdmin log in exactly as before this PR
/// (regression-critical — no selector, no behavior change). A user with 2+ dealers (home +
/// <see cref="UserDealerMembership"/> grants) gets a dealer list and a pending token instead of a
/// session token, picks via <c>POST auth/select-dealer</c>, and an already-authenticated multi-dealer
/// user re-scopes via <c>POST auth/switch-dealer</c> without logging out again.
/// </summary>
public class LoginUserCommandHandlerMultiDealerTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _context;
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly ITokenProvider _tokenProvider;
    private readonly JsonWebTokenHandler _jwtHandler = new();

    public LoginUserCommandHandlerMultiDealerTests()
    {
        _context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        // Real TokenProvider (not mocked): several assertions below decode the actual issued JWT to
        // check which claims are present/absent, which a Mock<ITokenProvider> cannot produce.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "supersecretkeysupersecretkeysupersecretkey",
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:ExpirationInMinutes"] = "60",
            ["Jwt:PendingSelectionExpirationInMinutes"] = "5"
        }).Build();
        _tokenProvider = new TokenProvider(configuration);

        _passwordHasher.Setup(h => h.Verify("correct", "hash")).Returns(true);
        _passwordHasher.Setup(h => h.Verify("wrong", "hash")).Returns(false);
    }

    private LoginUserCommandHandler LoginHandler() => new(_context, _passwordHasher.Object, _tokenProvider);

    // ---------------------------------------------------------------------
    // Scenario 1 (regression): single dealer, and SuperAdmin, never see a selector.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Login_SingleDealerUser_ReturnsSessionToken_NotASelector()
    {
        Guid dealerId = Guid.NewGuid();
        var role = new Role(dealerId, "Vendedor", "Vendedor");
        var user = new User(dealerId, "solo@autossur.com", "Solo", "Dealer", "hash", role.Id);
        _context.Roles.Add(role);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        Result<LoginResult> result = await LoginHandler()
            .Handle(new LoginUserCommand("solo@autossur.com", "correct"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().NotBeNullOrEmpty();
        result.Value.PendingToken.Should().BeNull();
        result.Value.AvailableDealers.Should().BeNull();

        var jwt = _jwtHandler.ReadJsonWebToken(result.Value.Token);
        jwt.Claims.First(c => c.Type == "dealer_id").Value.Should().Be(dealerId.ToString());
        jwt.Claims.First(c => c.Type == "role").Value.Should().Be("Vendedor");
    }

    [Fact]
    public async Task Login_SuperAdmin_NeverGetsASelector()
    {
        var superAdmin = User.CreateSuperAdmin("super@carstore.com", "Super", "Admin", "hash");
        _context.Users.Add(superAdmin);
        await _context.SaveChangesAsync();

        Result<LoginResult> result = await LoginHandler()
            .Handle(new LoginUserCommand("super@carstore.com", "correct"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().NotBeNullOrEmpty();
        result.Value.PendingToken.Should().BeNull();
        result.Value.AvailableDealers.Should().BeNull();

        var jwt = _jwtHandler.ReadJsonWebToken(result.Value.Token);
        jwt.Claims.First(c => c.Type == "platform_role").Value.Should().Be("super_admin");
        jwt.Claims.First(c => c.Type == "dealer_id").Value.Should().Be(Guid.Empty.ToString());
    }

    // ---------------------------------------------------------------------
    // Scenario 2: 2+ dealers -> selector instead of a session token.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Login_UserWithHomeDealerPlusOneMembership_ReturnsDealerListAndPendingToken_NotASessionToken()
    {
        (Guid dealerA, _, User user, Guid dealerB, _) = await SeedMultiDealerUserAsync();

        Result<LoginResult> result = await LoginHandler()
            .Handle(new LoginUserCommand("multi@dos-dealers.com", "correct"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().BeNull();
        result.Value.PendingToken.Should().NotBeNullOrEmpty();
        result.Value.AvailableDealers.Should().NotBeNull();
        result.Value.AvailableDealers!.Select(d => d.DealerId).Should().BeEquivalentTo(new[] { dealerA, dealerB });
    }

    [Fact]
    public async Task Login_PendingToken_CarriesNoDealerIdClaim()
    {
        (_, _, User user, _, _) = await SeedMultiDealerUserAsync();

        Result<LoginResult> result = await LoginHandler()
            .Handle(new LoginUserCommand("multi@dos-dealers.com", "correct"), CancellationToken.None);

        var jwt = _jwtHandler.ReadJsonWebToken(result.Value.PendingToken);
        jwt.Claims.Any(c => c.Type == "dealer_id").Should().BeFalse();
        jwt.Claims.Any(c => c.Type == "role").Should().BeFalse();
        jwt.Claims.First(c => c.Type == "login_stage").Value.Should().Be("pending_dealer_selection");
        jwt.Subject.Should().Be(user.Id.ToString());
    }

    // ---------------------------------------------------------------------
    // auth/select-dealer
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SelectDealer_WithPendingTokenAndOwnedDealer_IssuesRealSessionTokenScopedCorrectly()
    {
        (_, _, User user, Guid dealerB, Role roleB) = await SeedMultiDealerUserAsync();

        var handler = new SelectDealerCommandHandler(_context, PendingUserContext(user.Id).Object, _tokenProvider);
        Result<string> result = await handler.Handle(new SelectDealerCommand(dealerB), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var jwt = _jwtHandler.ReadJsonWebToken(result.Value);
        jwt.Claims.First(c => c.Type == "dealer_id").Value.Should().Be(dealerB.ToString());
        jwt.Claims.First(c => c.Type == "role").Value.Should().Be(roleB.Name);
        jwt.Claims.Any(c => c.Type == "login_stage").Should().BeFalse();
    }

    [Fact]
    public async Task SelectDealer_WithDealerUserDoesNotBelongTo_IsRejected_NoToken()
    {
        (_, _, User user, _, _) = await SeedMultiDealerUserAsync();
        Guid foreignDealerId = Guid.NewGuid();

        var handler = new SelectDealerCommandHandler(_context, PendingUserContext(user.Id).Object, _tokenProvider);
        Result<string> result = await handler.Handle(new SelectDealerCommand(foreignDealerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DealerAccessErrors.NotAMember);
    }

    [Fact]
    public async Task SelectDealer_WithNormalSessionToken_IsRejected_CannotReplayAsPending()
    {
        (_, _, User user, Guid dealerB, _) = await SeedMultiDealerUserAsync();

        var handler = new SelectDealerCommandHandler(_context, FullSessionUserContext(user.Id).Object, _tokenProvider);
        Result<string> result = await handler.Handle(new SelectDealerCommand(dealerB), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DealerAccessErrors.InvalidStage);
    }

    // ---------------------------------------------------------------------
    // auth/switch-dealer
    // ---------------------------------------------------------------------

    [Fact]
    public async Task SwitchDealer_FromAuthenticatedSession_RescopesToAnotherOwnedDealer()
    {
        (_, _, User user, Guid dealerB, Role roleB) = await SeedMultiDealerUserAsync();

        var handler = new SwitchDealerCommandHandler(_context, FullSessionUserContext(user.Id).Object, _tokenProvider);
        Result<string> result = await handler.Handle(new SwitchDealerCommand(dealerB), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var jwt = _jwtHandler.ReadJsonWebToken(result.Value);
        jwt.Claims.First(c => c.Type == "dealer_id").Value.Should().Be(dealerB.ToString());
        jwt.Claims.First(c => c.Type == "role").Value.Should().Be(roleB.Name);
    }

    [Fact]
    public async Task SwitchDealer_ToDealerUserDoesNotBelongTo_IsRejected()
    {
        (_, _, User user, _, _) = await SeedMultiDealerUserAsync();
        Guid foreignDealerId = Guid.NewGuid();

        var handler = new SwitchDealerCommandHandler(_context, FullSessionUserContext(user.Id).Object, _tokenProvider);
        Result<string> result = await handler.Handle(new SwitchDealerCommand(foreignDealerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DealerAccessErrors.NotAMember);
    }

    [Fact]
    public async Task SwitchDealer_WithPendingToken_IsRejected_MustUseSelectDealerInstead()
    {
        (_, _, User user, Guid dealerB, _) = await SeedMultiDealerUserAsync();

        var handler = new SwitchDealerCommandHandler(_context, PendingUserContext(user.Id).Object, _tokenProvider);
        Result<string> result = await handler.Handle(new SwitchDealerCommand(dealerB), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DealerAccessErrors.InvalidStage);
    }

    // ---------------------------------------------------------------------
    // Granting a membership (admin-gated).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GrantMembership_WithRoleFromAnotherDealer_IsRejected_SameErrorAsNonexistentRole()
    {
        Guid dealerId = Guid.NewGuid();
        Mock<ICurrentTenantService> tenant = TenantOf(dealerId);
        var clock = ClockAt(Now);

        var foreignRole = new Role(Guid.NewGuid(), "Admin", "Admin de otra concesionaria");
        var targetUser = new User(Guid.NewGuid(), "target@otra.com", "Target", "User", "hash", Guid.NewGuid());
        _context.Roles.Add(foreignRole);
        _context.Users.Add(targetUser);
        await _context.SaveChangesAsync();

        var handler = new GrantUserDealerMembershipCommandHandler(_context, _context, tenant.Object, clock.Object, AlwaysAllowEntitlements());
        Result<Guid> foreignResult = await handler.Handle(
            new GrantUserDealerMembershipCommand(targetUser.Id, foreignRole.Id), CancellationToken.None);
        Result<Guid> nonexistentResult = await handler.Handle(
            new GrantUserDealerMembershipCommand(targetUser.Id, Guid.NewGuid()), CancellationToken.None);

        foreignResult.IsFailure.Should().BeTrue();
        nonexistentResult.IsFailure.Should().BeTrue();
        foreignResult.Error.Code.Should().Be(nonexistentResult.Error.Code);
        foreignResult.Error.Type.Should().Be(nonexistentResult.Error.Type);
        foreignResult.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GrantMembership_UserAlreadyHasThisDealer_AsHomeDealer_IsRejected()
    {
        Guid dealerId = Guid.NewGuid();
        Mock<ICurrentTenantService> tenant = TenantOf(dealerId);
        var clock = ClockAt(Now);

        var role = new Role(dealerId, "Vendedor", "Vendedor");
        var homeUser = new User(dealerId, "home@mio.com", "Home", "User", "hash", role.Id);
        _context.Roles.Add(role);
        _context.Users.Add(homeUser);
        await _context.SaveChangesAsync();

        var handler = new GrantUserDealerMembershipCommandHandler(_context, _context, tenant.Object, clock.Object, AlwaysAllowEntitlements());
        Result<Guid> result = await handler.Handle(
            new GrantUserDealerMembershipCommand(homeUser.Id, role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DealerAccessErrors.AlreadyGranted(homeUser.Id, dealerId));
    }

    [Fact]
    public async Task GrantMembership_UserAlreadyHasThisDealer_AsExistingMembership_IsRejected()
    {
        Guid dealerId = Guid.NewGuid();
        Mock<ICurrentTenantService> tenant = TenantOf(dealerId);
        var clock = ClockAt(Now);

        var role = new Role(dealerId, "Vendedor", "Vendedor");
        var otherRole = new Role(dealerId, "Gerente", "Gerente");
        var foreignUser = new User(Guid.NewGuid(), "foreign@otra.com", "Foreign", "User", "hash", Guid.NewGuid());
        _context.Roles.AddRange(role, otherRole);
        _context.Users.Add(foreignUser);
        _context.UserDealerMemberships.Add(UserDealerMembership.Grant(foreignUser.Id, dealerId, role.Id, Now));
        await _context.SaveChangesAsync();

        var handler = new GrantUserDealerMembershipCommandHandler(_context, _context, tenant.Object, clock.Object, AlwaysAllowEntitlements());
        Result<Guid> result = await handler.Handle(
            new GrantUserDealerMembershipCommand(foreignUser.Id, otherRole.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DealerAccessErrors.AlreadyGranted(foreignUser.Id, dealerId));
    }

    [Fact]
    public async Task GrantMembership_Succeeds_ForAnExistingUserFromAnotherDealer()
    {
        Guid dealerId = Guid.NewGuid();
        Mock<ICurrentTenantService> tenant = TenantOf(dealerId);
        var clock = ClockAt(Now);

        var role = new Role(dealerId, "Vendedor", "Vendedor");
        var foreignUser = new User(Guid.NewGuid(), "foreign2@otra.com", "Foreign", "User", "hash", Guid.NewGuid());
        _context.Roles.Add(role);
        _context.Users.Add(foreignUser);
        await _context.SaveChangesAsync();

        var handler = new GrantUserDealerMembershipCommandHandler(_context, _context, tenant.Object, clock.Object, AlwaysAllowEntitlements());
        Result<Guid> result = await handler.Handle(
            new GrantUserDealerMembershipCommand(foreignUser.Id, role.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        UserDealerMembership membership = await _context.UserDealerMemberships
            .AsNoTracking().SingleAsync(m => m.Id == result.Value);
        membership.UserId.Should().Be(foreignUser.Id);
        membership.DealerId.Should().Be(dealerId);
        membership.RoleId.Should().Be(role.Id);
    }

    // ---------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------

    private static Mock<IUserContext> PendingUserContext(Guid userId)
    {
        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(c => c.UserId).Returns(userId);
        userContext.SetupGet(c => c.IsPendingDealerSelection).Returns(true);
        return userContext;
    }

    private static Mock<IUserContext> FullSessionUserContext(Guid userId)
    {
        var userContext = new Mock<IUserContext>();
        userContext.SetupGet(c => c.UserId).Returns(userId);
        userContext.SetupGet(c => c.IsPendingDealerSelection).Returns(false);
        return userContext;
    }

    private static Mock<ICurrentTenantService> TenantOf(Guid dealerId)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        return tenant;
    }

    private static Mock<IDateTimeProvider> ClockAt(DateTime now)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(now);
        return clock;
    }

    /// <summary>sfc A3.1: these tests exercise multi-dealer login/membership behavior, not entitlements.</summary>
    private static IEntitlementService AlwaysAllowEntitlements()
    {
        var mock = new Mock<IEntitlementService>();
        mock.Setup(s => s.EnsureCanAddAsync(It.IsAny<Guid>(), It.IsAny<PlanResource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        mock.Setup(s => s.RoleConsumesASeatAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return mock.Object;
    }

    /// <summary>Home dealer A (role A) + an extra <see cref="UserDealerMembership"/> grant in dealer B (role B).</summary>
    private async Task<(Guid DealerA, Role RoleA, User User, Guid DealerB, Role RoleB)> SeedMultiDealerUserAsync()
    {
        Guid dealerA = Guid.NewGuid();
        Guid dealerB = Guid.NewGuid();
        var roleA = new Role(dealerA, "Vendedor", "Vendedor");
        var roleB = new Role(dealerB, "Gerente", "Gerente");
        var user = new User(dealerA, "multi@dos-dealers.com", "Multi", "Dealer", "hash", roleA.Id);
        var dealerSettingsA = new DealerSettingsEntity(dealerA, "Autos Sur", "contacto@autossur.com");
        var dealerSettingsB = new DealerSettingsEntity(dealerB, "Autos Norte", "contacto@autosnorte.com");
        UserDealerMembership membership = UserDealerMembership.Grant(user.Id, dealerB, roleB.Id, Now);

        _context.Roles.AddRange(roleA, roleB);
        _context.Users.Add(user);
        _context.DealerSettings.AddRange(dealerSettingsA, dealerSettingsB);
        _context.UserDealerMemberships.Add(membership);
        await _context.SaveChangesAsync();

        return (dealerA, roleA, user, dealerB, roleB);
    }
}
