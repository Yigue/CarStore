using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Abstractions.Email;
using Application.Abstractions.Tenancy;
using Application.Platform.AuditLogs;
using Application.UnitTests;
using Application.Users.OwnerTransfer;
using Application.Users.OwnerTransfer.AcceptOwnerTransfer;
using Application.Users.OwnerTransfer.InitiateOwnerTransfer;
using Application.Users.Verification;
using Domain.Platform;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace ApplicationTests.Users;

/// <summary>
/// sfc A4.3 / A22 / RN-029 (design C17): the owner hands the dealership over with a double confirmation. The current
/// owner initiates, the recipient accepts with a single-use, hash-only, 48 h token. The target must be an active,
/// verified Admin of the SAME dealer (404 otherwise); on accept the owner is swapped, audited and the provider
/// billing email follows.
/// </summary>
public class OwnerTransferCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private const string StripeCustomer = "cus_owner";

    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly TestApplicationDbContext _context;
    private readonly Mock<ICurrentTenantService> _tenant = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<ISubscriptionGateway> _gateway = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly List<(EmailTemplate Template, string Recipient, IReadOnlyDictionary<string, string> Model)> _sent = [];
    private readonly Mock<ITransactionalEmailSender> _email = new();
    private Role _admin = null!;
    private Role _seller = null!;
    private User _owner = null!;
    private User _target = null!;
    private DealerSettingsEntity _dealer = null!;

    public OwnerTransferCommandHandlerTests()
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
        ActAs(_owner);
    }

    private void Seed()
    {
        _dealer = new DealerSettingsEntity(_dealerId, _dealerId, "Autos Sur SA", "contacto@autossur.com");
        _dealer.SetStripeCustomerId(StripeCustomer);
        _context.DealerSettings.Add(_dealer);
        _admin = new Role(_dealerId, "Admin", "Admin");
        _seller = new Role(_dealerId, "Vendedor", "Vendedor");
        _context.Roles.AddRange(_admin, _seller);
        _owner = NewUser(_dealerId, "owner@autossur.com", "Ana", _admin, verified: true);
        _target = NewUser(_dealerId, "nuevo@autossur.com", "Beto", _admin, verified: true);
        _dealer.SetOwner(_owner.Id);
        _context.SaveChanges();
    }

    private User NewUser(Guid dealerId, string email, string name, Role role, bool verified)
    {
        var user = new User(dealerId, email, name, "Apellido", "hash", role.Id);
        if (verified)
            user.MarkEmailVerified(Now.AddDays(-30));
        _context.Users.Add(user);
        return user;
    }

    private void ActAs(User user) => _user.SetupGet(u => u.UserId).Returns(user.Id);

    private InitiateOwnerTransferCommandHandler Initiate() =>
        new(_context, _tenant.Object, _user.Object, _email.Object,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.example.com/" }).Build(),
            _clock.Object, NullLogger<InitiateOwnerTransferCommandHandler>.Instance);

    private AcceptOwnerTransferCommandHandler Accept() =>
        new(_context, _tenant.Object, _user.Object, _gateway.Object, new PlatformAuditLogRecorder(_context), _clock.Object,
            NullLogger<AcceptOwnerTransferCommandHandler>.Instance);

    private async Task<string> IssueTokenAsync(
        User target, Guid? createdBy = null, UserActionPurpose purpose = UserActionPurpose.OwnerTransfer,
        Guid? dealerId = null, DateTime? issuedAt = null)
    {
        string plain = UserActionTokenHasher.NewToken();
        _context.UserActionTokens.Add(UserActionToken.Issue(
            [], purpose, dealerId ?? _dealerId, target.Email.Value, UserActionTokenHasher.Hash(plain),
            target.Id, null, createdBy ?? _owner.Id, issuedAt ?? Now));
        await _context.SaveChangesAsync();
        return plain;
    }

    private async Task<DealerSettingsEntity> StoredDealerAsync() =>
        await _context.DealerSettings.IgnoreQueryFilters().AsNoTracking().SingleAsync();

    // ---- Initiate -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Initiate_ByTheOwner_PersistsOnlyTheHashAndEmailsTheRecipient()
    {
        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(_target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _sent.Should().ContainSingle();
        var mail = _sent.Single();
        mail.Template.Should().Be(EmailTemplate.OwnerTransfer);
        mail.Recipient.Should().Be("nuevo@autossur.com");
        mail.Model["Name"].Should().Be("Beto");
        mail.Model["DealerName"].Should().Be("Autos Sur SA");
        mail.Model["ExpiresInHours"].Should().Be("48");
        mail.Model["ConfirmUrl"].Should().StartWith("https://app.example.com/");
        string plain = mail.Model["ConfirmUrl"].Split("token=")[1];

        UserActionToken stored = await _context.UserActionTokens.SingleAsync();
        stored.Purpose.Should().Be(UserActionPurpose.OwnerTransfer);
        stored.TokenHash.Should().Be(UserActionTokenHasher.Hash(plain));
        stored.TokenHash.Should().NotBe(plain);
        stored.UserId.Should().Be(_target.Id);
        stored.CreatedBy.Should().Be(_owner.Id);
        stored.DealerId.Should().Be(_dealerId);
        stored.ExpiresAtUtc.Should().Be(Now.AddHours(48));
        (await _context.UserActionTokens.Select(t => t.TokenHash).ToListAsync()).Should().NotContain(plain);
        (await StoredDealerAsync()).OwnerUserId.Should().Be(_owner.Id, "nothing changes until the recipient accepts");
    }

    [Fact]
    public async Task Initiate_ByAnAdminWhoIsNotTheOwner_IsForbidden()
    {
        User otherAdmin = NewUser(_dealerId, "otro@autossur.com", "Otro", _admin, verified: true);
        await _context.SaveChangesAsync();
        ActAs(otherAdmin);

        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(_target.Id), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.NotOwner);
        _sent.Should().BeEmpty();
        (await _context.UserActionTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Initiate_ByASeller_IsForbidden()
    {
        User seller = NewUser(_dealerId, "vende@autossur.com", "Vende", _seller, verified: true);
        await _context.SaveChangesAsync();
        ActAs(seller);

        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(_target.Id), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.NotOwner);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Initiate_WhenTheDealerHasNoOwnerYet_FailsClosed()
    {
        // Legacy row that was never backfilled: nobody is the owner, so nobody can start a transfer.
        var legacyId = Guid.NewGuid();
        _tenant.SetupGet(t => t.DealerId).Returns(legacyId);
        _context.DealerSettings.Add(new DealerSettingsEntity(legacyId, legacyId, "Legacy SA", "legacy@x.com"));
        Role role = new(legacyId, "Admin", "Admin");
        _context.Roles.Add(role);
        User legacyAdmin = NewUser(legacyId, "legacyadmin@x.com", "Lea", role, verified: true);
        User legacyTarget = NewUser(legacyId, "legacytarget@x.com", "Lalo", role, verified: true);
        await _context.SaveChangesAsync();
        ActAs(legacyAdmin);

        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(legacyTarget.Id), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.NotOwner);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Initiate_ToAUserOfAnotherDealer_Is404_AndSendsNothing()
    {
        var otherDealer = Guid.NewGuid();
        Role otherRole = new(otherDealer, "Admin", "Admin");
        _context.Roles.Add(otherRole);
        User foreign = NewUser(otherDealer, "foreign@otro.com", "Fer", otherRole, verified: true);
        await _context.SaveChangesAsync();

        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(foreign.Id), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
        _sent.Should().BeEmpty();
        (await _context.UserActionTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Initiate_ToAnUnknownUser_Is404()
    {
        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Initiate_ToYourself_IsRejected()
    {
        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(_owner.Id), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.SameUser);
        _sent.Should().BeEmpty();
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("not-admin")]
    [InlineData("unverified")]
    public async Task Initiate_ToAnIneligibleTarget_IsRejected(string reason)
    {
        User target = reason switch
        {
            "not-admin" => NewUser(_dealerId, "vende@autossur.com", "Vende", _seller, verified: true),
            "unverified" => NewUser(_dealerId, "sinverificar@autossur.com", "Sin", _admin, verified: false),
            _ => NewUser(_dealerId, "baja@autossur.com", "Baja", _admin, verified: true)
        };
        if (reason == "inactive")
            target.Deactivate();
        await _context.SaveChangesAsync();

        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(target.Id), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.TargetNotEligible);
        _sent.Should().BeEmpty();
        (await _context.UserActionTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Initiate_AgainRevokesThePendingTransfer_EvenToAnotherTarget()
    {
        User second = NewUser(_dealerId, "segundo@autossur.com", "Segundo", _admin, verified: true);
        await _context.SaveChangesAsync();
        string first = await IssueTokenAsync(_target);

        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(second.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        List<UserActionToken> tokens = await _context.UserActionTokens.ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.Single(t => t.TokenHash == UserActionTokenHasher.Hash(first)).ConsumedAtUtc.Should().NotBeNull();
        tokens.Count(t => t.IsPending(Now)).Should().Be(1);
    }

    [Fact]
    public async Task Initiate_WhenTheEmailCannotBeSent_FailsAndLeavesNoPendingToken()
    {
        _email
            .Setup(s => s.SendAsync(It.IsAny<EmailTemplate>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        Result result = await Initiate().Handle(new InitiateOwnerTransferCommand(_target.Id), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.EmailNotSent);
        (await _context.UserActionTokens.ToListAsync()).Should().OnlyContain(t => !t.IsPending(Now));
    }

    // ---- Accept ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Accept_SwapsTheOwner_KeepsTheFormerOwnerAsActiveAdmin_AuditsAndSyncsTheProviderEmail()
    {
        string plain = await IssueTokenAsync(_target);
        ActAs(_target);

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await StoredDealerAsync()).OwnerUserId.Should().Be(_target.Id);
        User former = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == _owner.Id);
        former.IsActive.Should().BeTrue();
        former.RoleId.Should().Be(_admin.Id, "the former owner keeps the previous role: nobody loses access");
        (await _context.UserActionTokens.SingleAsync()).ConsumedAtUtc.Should().Be(Now);
        _gateway.Verify(g => g.UpdateCustomerEmailAsync(StripeCustomer, "nuevo@autossur.com", It.IsAny<CancellationToken>()), Times.Once);

        PlatformAuditLogEntry audit = await _context.PlatformAuditLogs.IgnoreQueryFilters().SingleAsync();
        audit.Action.Should().Be(PlatformAuditAction.OwnerTransferred);
        audit.ActorKind.Should().Be(PlatformAuditActorKind.SelfService);
        audit.ActorUserId.Should().Be(_target.Id);
        audit.DealerId.Should().Be(_dealerId);
        audit.Reason.Should().Contain("owner@autossur.com").And.Contain("nuevo@autossur.com");
    }

    [Fact]
    public async Task Accept_IsSingleUse()
    {
        string plain = await IssueTokenAsync(_target);
        ActAs(_target);

        Result first = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);
        Result replay = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        replay.Error.Should().Be(OwnerTransferErrors.InvalidToken);
        (await _context.PlatformAuditLogs.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        _gateway.Verify(g => g.UpdateCustomerEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Accept_AfterTheFortyEightHours_IsRejected()
    {
        string plain = await IssueTokenAsync(_target, issuedAt: Now.AddHours(-49));
        ActAs(_target);

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.InvalidToken);
        (await StoredDealerAsync()).OwnerUserId.Should().Be(_owner.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    public async Task Accept_WithAnUnknownToken_IsRejected(string token)
    {
        ActAs(_target);

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(token), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.InvalidToken);
        (await StoredDealerAsync()).OwnerUserId.Should().Be(_owner.Id);
    }

    [Fact]
    public async Task Accept_ByAUserThatIsNotTheRecipient_IsRejected_AndTheTokenIsNotSpent()
    {
        string plain = await IssueTokenAsync(_target);
        User stranger = NewUser(_dealerId, "otro@autossur.com", "Otro", _admin, verified: true);
        await _context.SaveChangesAsync();
        ActAs(stranger);

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.InvalidToken);
        (await _context.UserActionTokens.SingleAsync()).ConsumedAtUtc.Should().BeNull();
        (await StoredDealerAsync()).OwnerUserId.Should().Be(_owner.Id);
    }

    [Fact]
    public async Task Accept_WithATokenOfAnotherDealer_IsRejected()
    {
        var otherDealer = Guid.NewGuid();
        Role otherRole = new(otherDealer, "Admin", "Admin");
        _context.Roles.Add(otherRole);
        User foreign = NewUser(otherDealer, "foreign@otro.com", "Fer", otherRole, verified: true);
        await _context.SaveChangesAsync();
        string plain = await IssueTokenAsync(foreign, createdBy: Guid.NewGuid(), dealerId: otherDealer);
        ActAs(foreign);

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.InvalidToken);
        (await StoredDealerAsync()).OwnerUserId.Should().Be(_owner.Id);
    }

    [Fact]
    public async Task Accept_WithATokenOfAnotherPurpose_IsRejected()
    {
        string plain = await IssueTokenAsync(_target, purpose: UserActionPurpose.EmailVerification);
        ActAs(_target);

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.InvalidToken);
        (await StoredDealerAsync()).OwnerUserId.Should().Be(_owner.Id);
    }

    [Fact]
    public async Task Accept_WhenTheInitiatorIsNoLongerTheOwner_IsRejected()
    {
        string plain = await IssueTokenAsync(_target);
        _dealer = await _context.DealerSettings.SingleAsync();
        User someoneElse = NewUser(_dealerId, "otro@autossur.com", "Otro", _admin, verified: true);
        await _context.SaveChangesAsync();
        _dealer.SetOwner(someoneElse.Id);
        await _context.SaveChangesAsync();
        ActAs(_target);

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.InvalidToken);
        (await StoredDealerAsync()).OwnerUserId.Should().Be(someoneElse.Id);
    }

    [Fact]
    public async Task Accept_WhenTheRecipientWasDeactivatedMeanwhile_IsRejected_AndNothingChanges()
    {
        string plain = await IssueTokenAsync(_target);
        User tracked = await _context.Users.SingleAsync(u => u.Id == _target.Id);
        tracked.Deactivate();
        await _context.SaveChangesAsync();
        ActAs(_target);

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.TargetNotEligible);
        (await StoredDealerAsync()).OwnerUserId.Should().Be(_owner.Id);
        (await _context.PlatformAuditLogs.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Accept_WhenTheProviderFails_TheTransferStillHolds()
    {
        string plain = await IssueTokenAsync(_target);
        ActAs(_target);
        _gateway
            .Setup(g => g.UpdateCustomerEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("stripe down"));

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await StoredDealerAsync()).OwnerUserId.Should().Be(_target.Id);
    }

    [Fact]
    public async Task Accept_WithoutAProviderCustomer_DoesNotCallTheGateway()
    {
        _dealer = await _context.DealerSettings.SingleAsync();
        _context.Entry(_dealer).Property(nameof(DealerSettingsEntity.StripeCustomerId)).CurrentValue = null;
        await _context.SaveChangesAsync();
        string plain = await IssueTokenAsync(_target);
        ActAs(_target);

        Result result = await Accept().Handle(new AcceptOwnerTransferCommand(plain), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _gateway.Verify(g => g.UpdateCustomerEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
