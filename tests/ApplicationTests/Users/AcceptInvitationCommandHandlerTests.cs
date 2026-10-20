using Application.Abstractions.Authentication;
using Application.UnitTests;
using Application.Users.Invitations;
using Application.Users.Invitations.AcceptInvitation;
using Application.Users.Verification;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace ApplicationTests.Users;

/// <summary>
/// sfc A4.2 / A14 / RN-031: accepting an invitation is anonymous — the token is the only identity proof.
/// Unknown, expired, used or wrong-purpose tokens all answer the same generic error and spend nothing.
/// A valid token creates the user ALREADY verified, under the token's dealer and role.
/// </summary>
public class AcceptInvitationCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private const string Password = "Passw0rd123!";

    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private Role _role = null!;

    public AcceptInvitationCommandHandlerTests()
    {
        _context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _dealerId);
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _role = new Role(_dealerId, "Vendedor", "Vendedor");
        _context.Roles.Add(_role);
        _context.SaveChanges();
    }

    private AcceptInvitationCommandHandler Handler() => new(_context, _hasher.Object, _clock.Object);

    private async Task<string> IssueTokenAsync(
        string email, Guid? roleId = null, Guid? dealerId = null,
        UserActionPurpose purpose = UserActionPurpose.Invitation, DateTime? issuedAt = null)
    {
        string plain = UserActionTokenHasher.NewToken();
        _context.UserActionTokens.Add(UserActionToken.Issue(
            [], purpose, dealerId ?? _dealerId, email, UserActionTokenHasher.Hash(plain),
            userId: null, roleId: roleId ?? _role.Id, createdBy: null, nowUtc: issuedAt ?? Now));
        await _context.SaveChangesAsync();
        return plain;
    }

    [Fact]
    public async Task Accept_WithAValidToken_CreatesAVerifiedUser_UnderTheTokensDealerAndRole()
    {
        string plain = await IssueTokenAsync("invitado@afuera.com");

        Result<Guid> result = await Handler().Handle(
            new AcceptInvitationCommand(plain, Password, "Nuevo", "Invitado"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        User created = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == result.Value);
        created.Email.Value.Should().Be("invitado@afuera.com");
        created.DealerId.Should().Be(_dealerId);
        created.RoleId.Should().Be(_role.Id);
        created.FirstName.Should().Be("Nuevo");
        created.LastName.Should().Be("Invitado");
        created.IsEmailVerified.Should().BeTrue("accepting the invitation email IS the verification");
        created.EmailVerifiedAtUtc.Should().Be(Now);
        (await _context.UserActionTokens.SingleAsync()).ConsumedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Accept_IsSingleUse()
    {
        string plain = await IssueTokenAsync("invitado@afuera.com");

        Result<Guid> first = await Handler().Handle(new AcceptInvitationCommand(plain, Password, "Nuevo", "Invitado"), CancellationToken.None);
        Result<Guid> replay = await Handler().Handle(new AcceptInvitationCommand(plain, Password, "Nuevo", "Invitado"), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        replay.Error.Should().Be(InvitationErrors.InvalidToken);
        (await _context.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Accept_WithAnExpiredToken_IsRejected_AndCreatesNoUser()
    {
        string plain = await IssueTokenAsync("invitado@afuera.com", issuedAt: Now.AddDays(-8));

        Result<Guid> result = await Handler().Handle(new AcceptInvitationCommand(plain, Password, "Nuevo", "Invitado"), CancellationToken.None);

        result.Error.Should().Be(InvitationErrors.InvalidToken);
        (await _context.Users.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    public async Task Accept_WithAnUnknownOrEmptyToken_IsRejected(string token)
    {
        Result<Guid> result = await Handler().Handle(new AcceptInvitationCommand(token, Password, "Nuevo", "Invitado"), CancellationToken.None);

        result.Error.Should().Be(InvitationErrors.InvalidToken);
        (await _context.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Accept_WithATokenOfAnotherPurpose_IsRejected()
    {
        string plain = await IssueTokenAsync("invitado@afuera.com", purpose: UserActionPurpose.EmailVerification);

        Result<Guid> result = await Handler().Handle(new AcceptInvitationCommand(plain, Password, "Nuevo", "Invitado"), CancellationToken.None);

        result.Error.Should().Be(InvitationErrors.InvalidToken);
        (await _context.Users.CountAsync()).Should().Be(0);
    }

    // Security review (sfc A4.2): a token must not be burned on a doomed accept — Email has a global unique
    // index, so without this check the token-consume would succeed and the User insert would then throw,
    // locking the invitee out with no way to retry (the one-time secret is gone).
    [Fact]
    public async Task Accept_WhenTheEmailWasClaimedSinceTheInviteWentOut_IsRejected_AndDoesNotConsumeTheToken()
    {
        string plain = await IssueTokenAsync("invitado@afuera.com");
        _context.Users.Add(new User(_dealerId, "invitado@afuera.com", "Otro", "Dueño", "hash", _role.Id));
        await _context.SaveChangesAsync();

        Result<Guid> result = await Handler().Handle(new AcceptInvitationCommand(plain, Password, "Nuevo", "Invitado"), CancellationToken.None);

        result.Error.Should().Be(InvitationErrors.InvalidToken);
        (await _context.UserActionTokens.SingleAsync()).ConsumedAtUtc.Should().BeNull("a doomed accept must not spend the one-time token");
        (await _context.Users.CountAsync()).Should().Be(1);
    }

    // Security review (sfc A4.2): a stale grant must not resurrect. The role or the inviter's own standing may
    // have changed in the days between the invite email going out and it being accepted.
    [Fact]
    public async Task Accept_WhenTheInvitedRoleWasDeletedSinceTheInviteWentOut_IsRejected()
    {
        var role = new Role(_dealerId, "Temporal", "Temporal");
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();
        string plain = await IssueTokenAsync("invitado@afuera.com", roleId: role.Id);
        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        Result<Guid> result = await Handler().Handle(new AcceptInvitationCommand(plain, Password, "Nuevo", "Invitado"), CancellationToken.None);

        result.Error.Should().Be(InvitationErrors.InvalidToken);
        (await _context.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Accept_WhenTheInviterNoLongerCanManageUsers_IsRejected()
    {
        var adminRole = new Role(_dealerId, "Admin", "Admin");
        adminRole.AddPermission("CanManageUsers");
        _context.Roles.Add(adminRole);
        var inviter = new User(_dealerId, "admin@dealer.com", "Admin", "Uno", "hash", adminRole.Id);
        _context.Users.Add(inviter);
        await _context.SaveChangesAsync();
        Guid inviterId = inviter.Id;
        string plain = UserActionTokenHasher.NewToken();
        _context.UserActionTokens.Add(UserActionToken.Issue(
            [], UserActionPurpose.Invitation, _dealerId, "invitado@afuera.com", UserActionTokenHasher.Hash(plain),
            userId: null, roleId: _role.Id, createdBy: inviterId, nowUtc: Now));
        await _context.SaveChangesAsync();

        // The inviter is deactivated after sending the invite (left the team, was removed).
        (await _context.Users.FirstAsync(u => u.Id == inviterId)).Deactivate();
        await _context.SaveChangesAsync();

        Result<Guid> result = await Handler().Handle(new AcceptInvitationCommand(plain, Password, "Nuevo", "Invitado"), CancellationToken.None);

        result.Error.Should().Be(InvitationErrors.InvalidToken);
        (await _context.Users.CountAsync()).Should().Be(1, "only the inviter, no invitee created");
    }

    [Fact]
    public void Validator_RejectsAWeakPasswordOrAnEmptyName()
    {
        var validator = new AcceptInvitationCommandValidator();

        validator.Validate(new AcceptInvitationCommand("tok", "weak", "Nuevo", "Invitado")).IsValid.Should().BeFalse();
        validator.Validate(new AcceptInvitationCommand("tok", Password, "", "Invitado")).IsValid.Should().BeFalse();
        validator.Validate(new AcceptInvitationCommand("tok", Password, "Nuevo", "")).IsValid.Should().BeFalse();
        validator.Validate(new AcceptInvitationCommand("", Password, "Nuevo", "Invitado")).IsValid.Should().BeFalse();
        validator.Validate(new AcceptInvitationCommand("tok", Password, "Nuevo", "Invitado")).IsValid.Should().BeTrue();
    }
}
