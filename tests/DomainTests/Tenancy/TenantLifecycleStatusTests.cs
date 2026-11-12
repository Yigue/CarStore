using Domain.DealerSettings;

namespace DomainTests.Tenancy;

public class TenantLifecycleStatusTests
{
    private static Domain.DealerSettings.DealerSettings CreateDealer()
        => new(dealerId: Guid.NewGuid(), dealerName: "Test Dealer", contactEmail: "test@dealer.com");

    [Fact]
    public void NewDealer_StartsActiveWithoutRetentionSchedule()
    {
        var dealer = CreateDealer();

        dealer.LifecycleStatus.Should().Be(TenantLifecycleStatus.Active);
        dealer.CancelledAt.Should().BeNull();
        dealer.PurgeScheduledAt.Should().BeNull();
    }

    [Fact]
    public void StartRetention_SchedulesPurgeNinetyDaysAfterCancellation()
    {
        var dealer = CreateDealer();
        var cancelledAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        dealer.StartRetention(cancelledAt, purgeAfterDays: 90);

        dealer.LifecycleStatus.Should().Be(TenantLifecycleStatus.CancelledGrace);
        dealer.CancelledAt.Should().Be(cancelledAt);
        dealer.PurgeScheduledAt.Should().Be(cancelledAt.AddDays(90));
    }

    [Fact]
    public void StartRetention_HonoursConfiguredRetentionDays()
    {
        var dealer = CreateDealer();
        var cancelledAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        dealer.StartRetention(cancelledAt, purgeAfterDays: 45);

        dealer.PurgeScheduledAt.Should().Be(cancelledAt.AddDays(45));
    }

    [Fact]
    public void StartRetention_WithNonPositiveDays_Throws()
    {
        var dealer = CreateDealer();

        var act = () => dealer.StartRetention(DateTime.UtcNow, purgeAfterDays: 0);

        act.Should().Throw<SharedKernel.DomainException>();
    }

    [Fact]
    public void StartRetention_IsIdempotent_KeepsFirstSchedule()
    {
        var dealer = CreateDealer();
        var first = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        dealer.StartRetention(first, 90);

        dealer.StartRetention(first.AddDays(5), 90);

        dealer.CancelledAt.Should().Be(first);
        dealer.PurgeScheduledAt.Should().Be(first.AddDays(90));
    }

    [Fact]
    public void ClearRetention_AfterReactivation_ClearsScheduleAndRestoresActive()
    {
        var dealer = CreateDealer();
        dealer.StartRetention(DateTime.UtcNow.AddDays(-10), 90);

        dealer.ClearRetention();

        dealer.LifecycleStatus.Should().Be(TenantLifecycleStatus.Active);
        dealer.CancelledAt.Should().BeNull();
        dealer.PurgeScheduledAt.Should().BeNull();
    }

    [Fact]
    public void ClearRetention_OnActiveDealer_IsNoOp()
    {
        var dealer = CreateDealer();

        dealer.ClearRetention();

        dealer.LifecycleStatus.Should().Be(TenantLifecycleStatus.Active);
    }

    [Fact]
    public void ClearRetention_OnPurgedDealer_DoesNotResurrect()
    {
        var dealer = CreateDealer();
        dealer.StartRetention(DateTime.UtcNow.AddDays(-100), 90);
        dealer.MarkPurged();

        dealer.ClearRetention();

        dealer.LifecycleStatus.Should().Be(TenantLifecycleStatus.Purged);
    }
}
