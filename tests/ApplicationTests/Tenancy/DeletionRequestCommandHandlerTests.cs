using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Abstractions.Tenancy;
using Application.Tenancy.Deletion;
using Domain.Billing;
using Domain.DealerSettings;
using Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Tenancy;

/// <summary>
/// RN-015 (A1.4): the owner asks to delete the account. It needs re-authentication and the slug typed by hand,
/// cancels the subscription and enters the same 30/90 day retention flow. Nothing is deleted on the spot.
/// </summary>
public class DeletionRequestCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private const string Slug = "autos-sur";
    private const string Password = "S3cret!pass";

    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly TestApplicationDbContext _context;
    private readonly Mock<ICurrentTenantService> _tenant = new();
    private readonly Mock<IUserContext> _user = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ISubscriptionGateway> _gateway = new();
    private readonly Mock<ITenantLifecycleCache> _cache = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public DeletionRequestCommandHandlerTests()
    {
        _context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options, _dealerId);
        _tenant.SetupGet(t => t.DealerId).Returns(_dealerId);
        _tenant.SetupGet(t => t.HasTenant).Returns(true);
        _user.SetupGet(u => u.UserId).Returns(_userId);
        _user.SetupGet(u => u.IsAuthenticated).Returns(true);
        _hasher.Setup(h => h.Verify(Password, "hash")).Returns(true);
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
    }

    private RequestTenantDeletionCommandHandler Handler(Dictionary<string, string?>? config = null) =>
        new(_context, _tenant.Object, _user.Object, _hasher.Object, _gateway.Object, _cache.Object, _clock.Object,
            new ConfigurationBuilder().AddInMemoryCollection(config ?? new Dictionary<string, string?>()).Build());

    private Domain.DealerSettings.DealerSettings SeedDealer(bool inGrace = false, string? slug = Slug, string roleName = "Admin")
    {
        var dealer = new Domain.DealerSettings.DealerSettings(
            _dealerId, _dealerId, "Autos Sur SA", "owner@autossur.com");
        if (slug is not null)
            dealer.ChangeSlug(slug, $"{slug}.carstore.com");
        if (inGrace)
            dealer.StartRetention(Now.AddDays(-1), 90);
        _context.DealerSettings.Add(dealer);
        var role = new Role(_dealerId, roleName, roleName);
        _context.Roles.Add(role);
        _context.Users.Add(new User(_dealerId, "owner@autossur.com", "Ana", "Owner", "hash", role.Id));
        _context.SaveChanges();
        // The user id used by the handler is the one from IUserContext.
        _user.SetupGet(u => u.UserId).Returns(_context.Users.Local.Single().Id);
        return dealer;
    }

    private DealerSubscription SeedSubscription(string? stripeSubscriptionId = "sub_123")
    {
        var sub = DealerSubscription.Create(_dealerId, "cus_1", stripeSubscriptionId, "plan");
        _context.DealerSubscriptions.Add(sub);
        _context.SaveChanges();
        return sub;
    }

    private async Task<Domain.DealerSettings.DealerSettings> StoredDealerAsync() =>
        await _context.DealerSettings.IgnoreQueryFilters().SingleAsync();

    [Fact]
    public async Task ConfirmedRequest_CancelsNowAndStartsTheSameRetentionFlow()
    {
        SeedDealer();
        var sub = SeedSubscription();

        var result = await Handler().Handle(new RequestTenantDeletionCommand(Password, Slug), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _gateway.Verify(g => g.CancelNowAsync("sub_123", It.IsAny<CancellationToken>()), Times.Once);
        var dealer = await StoredDealerAsync();
        dealer.LifecycleStatus.Should().Be(TenantLifecycleStatus.CancelledGrace);
        dealer.CancelledAt.Should().Be(Now);
        dealer.PurgeScheduledAt.Should().Be(Now.AddDays(90));
        (await _context.DealerSubscriptions.SingleAsync(s => s.Id == sub.Id)).Status.Should().Be(SubscriptionStatus.Cancelled);
        _cache.Verify(c => c.Invalidate(_dealerId), Times.AtLeastOnce);
        result.Value.PurgeScheduledAt.Should().Be(Now.AddDays(90));
    }

    [Fact]
    public async Task ConfirmedRequest_DoesNotDeleteAnyData()
    {
        SeedDealer();
        SeedSubscription();

        await Handler().Handle(new RequestTenantDeletionCommand(Password, Slug), CancellationToken.None);

        (await _context.Users.CountAsync()).Should().Be(1);
        (await _context.DealerSettings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HonoursConfiguredPurgeDays()
    {
        SeedDealer();
        SeedSubscription();

        await Handler(new() { ["Retention:PurgeAfterDays"] = "60" })
            .Handle(new RequestTenantDeletionCommand(Password, Slug), CancellationToken.None);

        (await StoredDealerAsync()).PurgeScheduledAt.Should().Be(Now.AddDays(60));
    }

    [Fact]
    public async Task WithoutProviderSubscription_StillEntersRetention_WithoutCallingTheGateway()
    {
        SeedDealer();

        var result = await Handler().Handle(new RequestTenantDeletionCommand(Password, Slug), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _gateway.Verify(g => g.CancelNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await StoredDealerAsync()).LifecycleStatus.Should().Be(TenantLifecycleStatus.CancelledGrace);
    }

    [Fact]
    public async Task NonOwner_IsForbidden_AndNothingChanges()
    {
        SeedDealer(roleName: "Vendedor");
        SeedSubscription();

        var result = await Handler().Handle(new RequestTenantDeletionCommand(Password, Slug), CancellationToken.None);

        result.Error.Should().Be(TenantDeletionErrors.NotOwner);
        _gateway.Verify(g => g.CancelNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await StoredDealerAsync()).LifecycleStatus.Should().Be(TenantLifecycleStatus.Active);
    }

    [Fact]
    public async Task WrongPassword_IsRejected_AndNothingChanges()
    {
        SeedDealer();
        SeedSubscription();

        var result = await Handler().Handle(new RequestTenantDeletionCommand("not-it", Slug), CancellationToken.None);

        result.Error.Should().Be(TenantDeletionErrors.ReauthenticationFailed);
        _gateway.Verify(g => g.CancelNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await StoredDealerAsync()).LifecycleStatus.Should().Be(TenantLifecycleStatus.Active);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("autos-norte")]
    [InlineData("AUTOS-SUR")]
    public async Task SlugNotTypedExactly_IsRejected(string typed)
    {
        SeedDealer();
        SeedSubscription();

        var result = await Handler().Handle(new RequestTenantDeletionCommand(Password, typed), CancellationToken.None);

        result.Error.Should().Be(TenantDeletionErrors.ConfirmationMismatch);
        _gateway.Verify(g => g.CancelNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await StoredDealerAsync()).LifecycleStatus.Should().Be(TenantLifecycleStatus.Active);
    }

    [Fact]
    public async Task DealerWithoutSlug_CanNotBeConfirmed()
    {
        SeedDealer(slug: null);

        var result = await Handler().Handle(new RequestTenantDeletionCommand(Password, ""), CancellationToken.None);

        result.Error.Should().Be(TenantDeletionErrors.ConfirmationMismatch);
    }

    [Fact]
    public async Task AlreadyUnderRetention_IsAConflict_AndDoesNotPushTheDateOrCallTheGateway()
    {
        SeedDealer(inGrace: true);
        SeedSubscription();

        var result = await Handler().Handle(new RequestTenantDeletionCommand(Password, Slug), CancellationToken.None);

        result.Error.Should().Be(TenantDeletionErrors.AlreadyInProgress);
        _gateway.Verify(g => g.CancelNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await StoredDealerAsync()).PurgeScheduledAt.Should().Be(Now.AddDays(-1).AddDays(90));
    }

    [Fact]
    public async Task WhenTheProviderFails_NoLifecycleChangeIsPersisted()
    {
        SeedDealer();
        SeedSubscription();
        _gateway
            .Setup(g => g.CancelNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("stripe down"));

        Func<Task> act = () => Handler().Handle(new RequestTenantDeletionCommand(Password, Slug), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await StoredDealerAsync()).LifecycleStatus.Should().Be(TenantLifecycleStatus.Active);
    }

    [Fact]
    public async Task AlreadyCancelledSubscription_DoesNotCallTheGatewayAgain()
    {
        SeedDealer();
        var sub = SeedSubscription();
        sub.Cancel();
        _context.SaveChanges();

        var result = await Handler().Handle(new RequestTenantDeletionCommand(Password, Slug), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _gateway.Verify(g => g.CancelNowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await StoredDealerAsync()).LifecycleStatus.Should().Be(TenantLifecycleStatus.CancelledGrace);
    }
}
