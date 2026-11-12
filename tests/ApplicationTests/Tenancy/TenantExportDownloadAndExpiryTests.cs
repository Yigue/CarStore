using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Storage;
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

/// <summary>RN-014 (A7): authenticated download through a 15 minute presigned URL, and the 7 day expiry.</summary>
public class TenantExportDownloadAndExpiryTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly Mock<IStorageService> _storage = new();

    private TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options, _dealerId);

    private static Mock<IDateTimeProvider> Clock(DateTime now)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(now);
        return clock;
    }

    private GetTenantExportDownloadUrlQueryHandler DownloadHandler(TestApplicationDbContext context, DateTime? now = null)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(_dealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        _storage
            .Setup(s => s.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, TimeSpan _, CancellationToken _) => new Uri($"https://files.test/{key}?sig=1"));
        return new GetTenantExportDownloadUrlQueryHandler(context, tenant.Object, _storage.Object, Clock(now ?? Now).Object);
    }

    private TenantExport SeedReady(TestApplicationDbContext context, Guid? dealerId = null, DateTime? readyAt = null)
    {
        var at = readyAt ?? Now;
        var export = TenantExport.Request(dealerId ?? _dealerId, Guid.NewGuid(), at);
        export.MarkRunning(at);
        export.MarkReady($"exports/{export.DealerId}/{export.Id}.zip", new string('a', 64), 10, at);
        context.TenantExports.Add(export);
        context.SaveChanges();
        return export;
    }

    private void SeedDealer(TestApplicationDbContext context, bool purged = false)
    {
        var dealer = new Domain.DealerSettings.DealerSettings(_dealerId, "Dealer", "d@dealer.com");
        if (purged)
            dealer.MarkPurged();
        context.DealerSettings.Add(dealer);
        context.SaveChanges();
    }

    [Fact]
    public async Task Download_ReadyExport_ReturnsFifteenMinutePresignedUrl()
    {
        using var context = CreateContext();
        SeedDealer(context);
        var export = SeedReady(context);

        var result = await DownloadHandler(context).Handle(new GetTenantExportDownloadUrlQuery(export.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ToString().Should().Contain(export.ObjectKey!);
        _storage.Verify(s => s.GetPresignedUrlAsync(export.ObjectKey!, TimeSpan.FromMinutes(15), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Download_ExpiredStatus_ReturnsNotFound()
    {
        using var context = CreateContext();
        SeedDealer(context);
        var export = SeedReady(context);
        export.Expire(Now);
        context.SaveChanges();

        var result = await DownloadHandler(context).Handle(new GetTenantExportDownloadUrlQuery(export.Id), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Download_ReadyButPastExpiry_ReturnsNotFound_WithoutWaitingForTheSweep()
    {
        using var context = CreateContext();
        SeedDealer(context);
        var export = SeedReady(context);

        var result = await DownloadHandler(context, Now.AddDays(8))
            .Handle(new GetTenantExportDownloadUrlQuery(export.Id), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Download_ExportOfAnotherTenant_ReturnsNotFound()
    {
        using var context = CreateContext();
        SeedDealer(context);
        var export = SeedReady(context, Guid.NewGuid());

        var result = await DownloadHandler(context).Handle(new GetTenantExportDownloadUrlQuery(export.Id), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Download_WhenTenantIsPurged_ReturnsNotFound()
    {
        using var context = CreateContext();
        SeedDealer(context, purged: true);
        var export = SeedReady(context);

        var result = await DownloadHandler(context).Handle(new GetTenantExportDownloadUrlQuery(export.Id), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Download_UnknownId_ReturnsNotFound()
    {
        using var context = CreateContext();
        SeedDealer(context);

        var result = await DownloadHandler(context).Handle(new GetTenantExportDownloadUrlQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Expire_MarksOnlyReadyExportsPastTheirExpiryAndDeletesTheirObject()
    {
        using var context = CreateContext();
        var old = SeedReady(context, readyAt: Now.AddDays(-8));
        var fresh = SeedReady(context, readyAt: Now.AddDays(-1));
        var handler = new ExpireTenantExportsCommandHandler(context, _storage.Object, Clock(Now).Object);

        var result = await handler.Handle(new ExpireTenantExportsCommand(), CancellationToken.None);

        result.Value.Should().Be(1);
        (await context.TenantExports.FindAsync(old.Id))!.Status.Should().Be(TenantExportStatus.Expired);
        (await context.TenantExports.FindAsync(fresh.Id))!.Status.Should().Be(TenantExportStatus.Ready);
        _storage.Verify(s => s.DeleteFileAsync(old.ObjectKey!, It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.DeleteFileAsync(fresh.ObjectKey!, It.IsAny<CancellationToken>()), Times.Never);
    }
}
