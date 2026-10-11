using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Tenancy.Export;
using Domain.DealerSettings;
using Domain.Tenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Tenancy;

/// <summary>RN-014 (A7): request rules: at most one active export and one every 24 hours.</summary>
public class RequestTenantExportCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, _dealerId);

    private RequestTenantExportCommandHandler CreateHandler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(_dealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        var user = new Mock<IUserContext>();
        user.SetupGet(u => u.UserId).Returns(_userId);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(Now);
        return new RequestTenantExportCommandHandler(context, tenant.Object, user.Object, clock.Object);
    }

    private Domain.DealerSettings.DealerSettings SeedDealer(
        TestApplicationDbContext context, TenantLifecycleStatus lifecycle = TenantLifecycleStatus.Active)
    {
        var dealer = new Domain.DealerSettings.DealerSettings(_dealerId, "Dealer", "d@dealer.com");
        if (lifecycle == TenantLifecycleStatus.CancelledGrace)
            dealer.StartRetention(Now.AddDays(-1), 90);
        if (lifecycle == TenantLifecycleStatus.Purged)
            dealer.MarkPurged();
        context.DealerSettings.Add(dealer);
        context.SaveChanges();
        return dealer;
    }

    private TenantExport SeedExport(
        TestApplicationDbContext context, Guid dealerId, DateTime requestedAt, string finalState)
    {
        var export = TenantExport.Request(dealerId, _userId, requestedAt);
        switch (finalState)
        {
            case "running":
                export.MarkRunning(requestedAt);
                break;
            case "ready":
                export.MarkRunning(requestedAt);
                export.MarkReady("k", new string('a', 64), 1, requestedAt);
                break;
            case "failed":
                export.MarkRunning(requestedAt);
                export.Fail("boom", requestedAt);
                break;
        }

        context.TenantExports.Add(export);
        context.SaveChanges();
        return export;
    }

    [Fact]
    public async Task Handle_FirstRequest_CreatesRequestedExportForCallerAndTenant()
    {
        using var context = CreateContext();
        SeedDealer(context);

        var result = await CreateHandler(context).Handle(new RequestTenantExportCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var export = await context.TenantExports.SingleAsync();
        export.Id.Should().Be(result.Value);
        export.DealerId.Should().Be(_dealerId);
        export.RequestedBy.Should().Be(_userId);
        export.Status.Should().Be(TenantExportStatus.Requested);
        export.RequestedAtUtc.Should().Be(Now);
    }

    [Theory]
    [InlineData("requested")]
    [InlineData("running")]
    public async Task Handle_WhenAnotherExportIsActive_ReturnsConflict(string state)
    {
        using var context = CreateContext();
        SeedDealer(context);
        SeedExport(context, _dealerId, Now.AddDays(-3), state);

        var result = await CreateHandler(context).Handle(new RequestTenantExportCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TenantExportErrors.AlreadyInProgress);
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_WhenLastExportWasRequestedWithin24Hours_ReturnsRateLimited()
    {
        using var context = CreateContext();
        SeedDealer(context);
        SeedExport(context, _dealerId, Now.AddHours(-23), "ready");

        var result = await CreateHandler(context).Handle(new RequestTenantExportCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TenantExportErrors.RateLimited);
    }

    [Fact]
    public async Task Handle_WhenLastExportIsOlderThan24Hours_Succeeds()
    {
        using var context = CreateContext();
        SeedDealer(context);
        SeedExport(context, _dealerId, Now.AddHours(-25), "ready");

        var result = await CreateHandler(context).Handle(new RequestTenantExportCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FailedExportWithin24Hours_DoesNotBlockARetry()
    {
        using var context = CreateContext();
        SeedDealer(context);
        SeedExport(context, _dealerId, Now.AddHours(-1), "failed");

        var result = await CreateHandler(context).Handle(new RequestTenantExportCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_IgnoresExportsOfOtherTenants()
    {
        using var context = CreateContext();
        SeedDealer(context);
        SeedExport(context, Guid.NewGuid(), Now.AddMinutes(-5), "running");

        var result = await CreateHandler(context).Handle(new RequestTenantExportCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InCancelledGrace_StillAllowsExport()
    {
        using var context = CreateContext();
        SeedDealer(context, TenantLifecycleStatus.CancelledGrace);

        var result = await CreateHandler(context).Handle(new RequestTenantExportCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenTenantIsPurged_ReturnsNotFound_ReadingLifecycleFromTheDatabase()
    {
        using var context = CreateContext();
        SeedDealer(context, TenantLifecycleStatus.Purged);

        var result = await CreateHandler(context).Handle(new RequestTenantExportCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        (await context.TenantExports.CountAsync()).Should().Be(0);
    }
}
