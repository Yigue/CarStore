using Application.Abstractions.Authentication;
using Application.Abstractions.Email;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Application.UnitTests;
using Application.Users.Invitations;
using Application.Users.Invitations.InviteUser;
using Application.Users.Verification;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace ApplicationTests.Users;

/// <summary>
/// sfc A4.2 / A14 / RN-031: inviting someone by email. A foreign/nonexistent RoleId is the same 404 as
/// spr PR 0.2 (REV-SDD-03); a verified email anywhere on the platform is a 409, but an unverified/pending
/// one must not be revealed; the emailed token is single-use and 7 days.
/// </summary>
public class InviteUserCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly TestApplicationDbContext _context;
    private readonly Mock<ICurrentTenantService> _tenant = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly List<(EmailTemplate Template, string Recipient, IReadOnlyDictionary<string, string> Model)> _sent = [];
    private readonly Mock<ITransactionalEmailSender> _email = new();
    private Role _admin = null!;
    private User _inviter = null!;
    private DealerSettingsEntity _dealer = null!;

    public InviteUserCommandHandlerTests()
    {
        _context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            _dealerId);
        _tenant.SetupGet(t => t.DealerId).Returns(_dealerId);
        _tenant.SetupGet(t => t.HasTenant).Returns(true);
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _email
            .Setup(s => s.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<EmailTemplate, string, IReadOnlyDictionary<string, string>, CancellationToken>((t, r, m, _) => _sent.Add((t, r, m)))
            .Returns(Task.CompletedTask);
        Seed();
        _user.SetupGet(u => u.UserId).Returns(_inviter.Id);
    }

    private void Seed()
    {
        _dealer = new DealerSettingsEntity(_dealerId, _dealerId, "Autos Sur SA", "contacto@autossur.com");
        _context.DealerSettings.Add(_dealer);
        _admin = new Role(_dealerId, "Admin", "Admin");
        _context.Roles.Add(_admin);
        _inviter = new User(_dealerId, "ana@autossur.com", "Ana", "Gomez", "hash", _admin.Id);
        _inviter.MarkEmailVerified(Now.AddDays(-30));
        _context.Users.Add(_inviter);
        _context.SaveChanges();
    }

    /// <summary>sfc A3.1: these tests exercise pre-existing invitation behavior, not entitlements — always allow.</summary>
    private static IEntitlementService AlwaysAllowEntitlements()
    {
        var mock = new Mock<IEntitlementService>();
        mock.Setup(s => s.EnsureCanAddAsync(It.IsAny<Guid>(), It.IsAny<PlanResource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return mock.Object;
    }

    private InviteUserCommandHandler Handler() =>
        new(_context, _context, _tenant.Object, _user.Object, _email.Object,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.example.com/" }).Build(),
            _clock.Object, NullLogger<InviteUserCommandHandler>.Instance, AlwaysAllowEntitlements());

    [Fact]
    public async Task Invite_WithARoleOfAnotherDealer_Is404_IdenticalToANonexistentRole()
    {
        var otherDealerRole = new Role(Guid.NewGuid(), "Admin", "Admin de otra concesionaria");
        _context.Roles.Add(otherDealerRole);
        await _context.SaveChangesAsync();
        var nonexistentRoleId = Guid.NewGuid();

        Result foreignResult = await Handler().Handle(new InviteUserCommand("nuevo@autossur.com", otherDealerRole.Id), CancellationToken.None);
        Result nonexistentResult = await Handler().Handle(new InviteUserCommand("nuevo@autossur.com", nonexistentRoleId), CancellationToken.None);

        foreignResult.IsFailure.Should().BeTrue();
        foreignResult.Error.Should().Be(RoleErrors.NotFound(otherDealerRole.Id));
        foreignResult.Error.Type.Should().Be(ErrorType.NotFound);
        nonexistentResult.Error.Code.Should().Be(foreignResult.Error.Code);
        nonexistentResult.Error.Type.Should().Be(foreignResult.Error.Type);
        _sent.Should().BeEmpty();
        (await _context.UserActionTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Invite_WithARoleOfTheOwnDealer_EmailsATokenAndPersistsOnlyTheHash()
    {
        Result result = await Handler().Handle(new InviteUserCommand("nuevo@afuera.com", _admin.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _sent.Should().ContainSingle();
        var mail = _sent.Single();
        mail.Template.Should().Be(EmailTemplate.Invitation);
        mail.Recipient.Should().Be("nuevo@afuera.com");
        mail.Model["InviterName"].Should().Be("Ana");
        mail.Model["DealerName"].Should().Be("Autos Sur SA");
        mail.Model["ExpiresInDays"].Should().Be("7");
        mail.Model["AcceptUrl"].Should().StartWith("https://app.example.com/invitacion/aceptar?t=");
        string plain = mail.Model["AcceptUrl"].Split("t=")[1];

        UserActionToken stored = await _context.UserActionTokens.SingleAsync();
        stored.Purpose.Should().Be(UserActionPurpose.Invitation);
        stored.TokenHash.Should().Be(UserActionTokenHasher.Hash(plain));
        stored.TokenHash.Should().NotBe(plain);
        stored.RoleId.Should().Be(_admin.Id);
        stored.UserId.Should().BeNull();
        stored.DealerId.Should().Be(_dealerId);
        stored.ExpiresAtUtc.Should().Be(Now.AddDays(7));
        stored.Email.Should().Be("nuevo@afuera.com");
    }

    [Fact]
    public async Task Invite_WhenAVerifiedUserWithThatEmailExistsInAnotherDealer_Is409_AndSendsNothing()
    {
        var otherDealer = Guid.NewGuid();
        Role otherRole = new(otherDealer, "Admin", "Admin");
        _context.Roles.Add(otherRole);
        User verifiedElsewhere = new(otherDealer, "verificada@otro.com", "Vera", "Ficada", "hash", otherRole.Id);
        verifiedElsewhere.MarkEmailVerified(Now.AddDays(-1));
        _context.Users.Add(verifiedElsewhere);
        await _context.SaveChangesAsync();

        Result result = await Handler().Handle(new InviteUserCommand("verificada@otro.com", _admin.Id), CancellationToken.None);

        result.Error.Should().Be(UserErrors.EmailNotUnique);
        result.Error.Type.Should().Be(ErrorType.Conflict);
        _sent.Should().BeEmpty();
        (await _context.UserActionTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Invite_WhenAnUnverifiedAccountWithThatEmailExistsElsewhere_DoesNotLeakIt_AndSucceeds()
    {
        var otherDealer = Guid.NewGuid();
        Role otherRole = new(otherDealer, "Admin", "Admin");
        _context.Roles.Add(otherRole);
        User unverifiedElsewhere = new(otherDealer, "pendiente@otro.com", "Pen", "Diente", "hash", otherRole.Id);
        _context.Users.Add(unverifiedElsewhere);
        await _context.SaveChangesAsync();

        Result result = await Handler().Handle(new InviteUserCommand("pendiente@otro.com", _admin.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Invite_AgainForTheSameEmailAndDealer_RevokesThePreviousPendingInvitation()
    {
        Result first = await Handler().Handle(new InviteUserCommand("nuevo@afuera.com", _admin.Id), CancellationToken.None);
        string firstPlain = _sent.Single().Model["AcceptUrl"].Split("t=")[1];
        _sent.Clear();

        Result second = await Handler().Handle(new InviteUserCommand("nuevo@afuera.com", _admin.Id), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        List<UserActionToken> tokens = await _context.UserActionTokens.ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Single(t => t.TokenHash == UserActionTokenHasher.Hash(firstPlain)).ConsumedAtUtc.Should().NotBeNull();
        tokens.Count(t => t.IsPending(Now)).Should().Be(1);
    }

    [Fact]
    public async Task Invite_WhenAnotherDealerHasAPendingInvitationForTheSameEmail_DoesNotRevokeIt()
    {
        var otherDealer = Guid.NewGuid();
        Role otherRole = new(otherDealer, "Admin", "Admin");
        _context.Roles.Add(otherRole);
        _context.DealerSettings.Add(new DealerSettingsEntity(otherDealer, otherDealer, "Otra SA", "otra@x.com"));
        await _context.SaveChangesAsync();
        var otherTenant = new Mock<ICurrentTenantService>();
        otherTenant.SetupGet(t => t.DealerId).Returns(otherDealer);
        var otherUser = new Mock<IUserContext>();
        User otherInviter = new(otherDealer, "inviter2@otro.com", "Otro", "Inviter", "hash", otherRole.Id);
        otherInviter.MarkEmailVerified(Now.AddDays(-10));
        _context.Users.Add(otherInviter);
        await _context.SaveChangesAsync();
        otherUser.SetupGet(u => u.UserId).Returns(otherInviter.Id);
        var otherHandler = new InviteUserCommandHandler(
            _context, _context, otherTenant.Object, otherUser.Object, _email.Object,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.example.com/" }).Build(),
            _clock.Object, NullLogger<InviteUserCommandHandler>.Instance, AlwaysAllowEntitlements());
        await otherHandler.Handle(new InviteUserCommand("compartido@afuera.com", otherRole.Id), CancellationToken.None);
        _sent.Clear();

        Result result = await Handler().Handle(new InviteUserCommand("compartido@afuera.com", _admin.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        List<UserActionToken> tokens = await _context.UserActionTokens.Where(t => t.Email == "compartido@afuera.com").ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Count(t => t.IsPending(Now)).Should().Be(2, "each dealer's pending invitation is independent");
    }

    [Fact]
    public async Task Invite_WhenTheEmailCannotBeSent_FailsAndLeavesNoPendingToken()
    {
        _email
            .Setup(s => s.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        Result result = await Handler().Handle(new InviteUserCommand("nuevo@afuera.com", _admin.Id), CancellationToken.None);

        result.Error.Should().Be(InvitationErrors.EmailNotSent);
        (await _context.UserActionTokens.ToListAsync()).Should().OnlyContain(t => !t.IsPending(Now));
    }
}
