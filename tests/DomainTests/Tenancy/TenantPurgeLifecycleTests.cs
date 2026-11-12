using Domain.DealerSettings;
using Domain.Tenancy;

namespace DomainTests.Tenancy;

/// <summary>RN-013 (A1.4): CancelledGrace -> Locked -> Purged. Purge is only reachable from a due Locked tenant.</summary>
public class TenantPurgeLifecycleTests
{
    private static readonly DateTime Cancelled = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Domain.DealerSettings.DealerSettings Dealer()
    {
        var d = new Domain.DealerSettings.DealerSettings(
            Guid.NewGuid(), "Autos Sur SA", "owner@autossur.com",
            address: "Calle 1", phoneNumber: "123", facebookUrl: "fb", instagramUrl: "ig", twitterUrl: "tw");
        return d;
    }

    private static Domain.DealerSettings.DealerSettings InGrace()
    {
        var d = Dealer();
        d.StartRetention(Cancelled, purgeAfterDays: 90);
        return d;
    }

    [Fact]
    public void LockAfterGrace_BeforeGraceEnds_StaysInGrace()
    {
        var d = InGrace();

        d.LockAfterGrace(Cancelled.AddDays(29), graceDays: 30).Should().BeFalse();

        d.LifecycleStatus.Should().Be(TenantLifecycleStatus.CancelledGrace);
    }

    [Fact]
    public void LockAfterGrace_AfterGraceEnds_Locks()
    {
        var d = InGrace();

        d.LockAfterGrace(Cancelled.AddDays(30), graceDays: 30).Should().BeTrue();

        d.LifecycleStatus.Should().Be(TenantLifecycleStatus.Locked);
        d.PurgeScheduledAt.Should().Be(Cancelled.AddDays(90));
    }

    [Fact]
    public void LockAfterGrace_OnActiveTenant_IsNoOp()
    {
        var d = Dealer();

        d.LockAfterGrace(DateTime.UtcNow.AddYears(5), graceDays: 30).Should().BeFalse();

        d.LifecycleStatus.Should().Be(TenantLifecycleStatus.Active);
    }

    [Fact]
    public void ClearRetention_OnLockedTenant_DoesNotResurrect()
    {
        var d = InGrace();
        d.LockAfterGrace(Cancelled.AddDays(31), 30);

        d.ClearRetention();

        d.LifecycleStatus.Should().Be(TenantLifecycleStatus.Locked);
    }

    [Theory]
    [InlineData(TenantLifecycleStatus.Active)]
    [InlineData(TenantLifecycleStatus.CancelledGrace)]
    public void IsDueForPurge_IsFalseUnlessLocked(TenantLifecycleStatus status)
    {
        var d = Dealer();
        if (status == TenantLifecycleStatus.CancelledGrace)
            d.StartRetention(Cancelled, 90);

        d.IsDueForPurge(Cancelled.AddDays(400)).Should().BeFalse();
    }

    [Fact]
    public void IsDueForPurge_LockedButNotYetDue_IsFalse()
    {
        var d = InGrace();
        d.LockAfterGrace(Cancelled.AddDays(31), 30);

        d.IsDueForPurge(Cancelled.AddDays(89)).Should().BeFalse();
        d.IsDueForPurge(Cancelled.AddDays(90)).Should().BeTrue();
    }

    [Fact]
    public void Purge_WhenNotDue_Throws()
    {
        var d = InGrace();
        d.LockAfterGrace(Cancelled.AddDays(31), 30);

        var act = () => d.Purge(Cancelled.AddDays(60));

        act.Should().Throw<SharedKernel.DomainException>();
        d.LifecycleStatus.Should().Be(TenantLifecycleStatus.Locked);
    }

    [Fact]
    public void Purge_WhenDue_AnonymizesContactDataAndKeepsFiscalName()
    {
        var d = InGrace();
        d.LockAfterGrace(Cancelled.AddDays(31), 30);

        d.Purge(Cancelled.AddDays(90));

        d.LifecycleStatus.Should().Be(TenantLifecycleStatus.Purged);
        d.DealerName.Should().Be("Autos Sur SA");
        d.ContactEmail.Should().NotBe("owner@autossur.com");
        d.Address.Should().BeNull();
        d.PhoneNumber.Should().BeNull();
        d.FacebookUrl.Should().BeNull();
        d.InstagramUrl.Should().BeNull();
        d.TwitterUrl.Should().BeNull();
        d.IsActive.Should().BeFalse();
    }
}

public class TenantPurgeRecordTests
{
    [Fact]
    public void Create_StoresCountsAndDealer()
    {
        var dealerId = Guid.NewGuid();
        var at = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var record = TenantPurgeRecord.Create(dealerId, "Autos Sur SA", at, new Dictionary<string, int> { ["cars"] = 3, ["clients"] = 2 });

        record.DealerId.Should().Be(dealerId);
        record.PurgedAtUtc.Should().Be(at);
        record.Counts.Should().Contain("cars", 3).And.Contain("clients", 2);
    }
}
