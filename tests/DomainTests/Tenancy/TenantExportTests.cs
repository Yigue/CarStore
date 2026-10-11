using Domain.Tenancy;
using SharedKernel;

namespace DomainTests.Tenancy;

/// <summary>RN-014 (A7): lifecycle of a tenant data export: Requested, Running, Ready, then Expired or Failed.</summary>
public class TenantExportTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    private static TenantExport NewExport() => TenantExport.Request(Guid.NewGuid(), Guid.NewGuid(), Now);

    [Fact]
    public void Request_StartsRequestedAndKeepsTenantAndRequester()
    {
        var dealerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var export = TenantExport.Request(dealerId, userId, Now);

        export.Status.Should().Be(TenantExportStatus.Requested);
        export.DealerId.Should().Be(dealerId);
        export.RequestedBy.Should().Be(userId);
        export.RequestedAtUtc.Should().Be(Now);
        export.ObjectKey.Should().BeNull();
        export.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Request_WithEmptyDealer_Throws()
    {
        var act = () => TenantExport.Request(Guid.Empty, Guid.NewGuid(), Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void HappyPath_RequestedRunningReady_SetsObjectAndSevenDayExpiry()
    {
        var export = NewExport();

        export.MarkRunning(Now);
        export.MarkReady("exports/d/e.zip", new string('a', 64), 1234, Now.AddMinutes(1));

        export.Status.Should().Be(TenantExportStatus.Ready);
        export.ObjectKey.Should().Be("exports/d/e.zip");
        export.Sha256.Should().Be(new string('a', 64));
        export.SizeBytes.Should().Be(1234);
        export.ExpiresAt.Should().Be(Now.AddMinutes(1).AddDays(7));
    }

    [Fact]
    public void MarkReady_FromRequested_Throws()
    {
        var export = NewExport();

        var act = () => export.MarkReady("k", new string('a', 64), 1, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Expire_FromReady_Expires()
    {
        var export = NewExport();
        export.MarkRunning(Now);
        export.MarkReady("k", new string('a', 64), 1, Now);

        export.Expire(Now.AddDays(8));

        export.Status.Should().Be(TenantExportStatus.Expired);
    }

    [Fact]
    public void Expire_FromRequested_Throws()
    {
        var act = () => NewExport().Expire(Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Fail_FromRunning_RecordsReason()
    {
        var export = NewExport();
        export.MarkRunning(Now);

        export.Fail("boom", Now);

        export.Status.Should().Be(TenantExportStatus.Failed);
        export.FailureReason.Should().Be("boom");
    }

    [Fact]
    public void Fail_FromReady_Throws()
    {
        var export = NewExport();
        export.MarkRunning(Now);
        export.MarkReady("k", new string('a', 64), 1, Now);

        var act = () => export.Fail("late", Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void IsActive_IsTrueOnlyWhileRequestedOrRunning()
    {
        var export = NewExport();
        export.IsActive.Should().BeTrue();
        export.MarkRunning(Now);
        export.IsActive.Should().BeTrue();
        export.Fail("x", Now);
        export.IsActive.Should().BeFalse();
    }
}
