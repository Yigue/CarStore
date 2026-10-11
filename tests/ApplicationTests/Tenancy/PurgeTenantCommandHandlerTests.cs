using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Tenancy.Purge;
using Domain.Billing;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.DealerSettings;
using Domain.Documents;
using Domain.Leads;
using Domain.Legal;
using Domain.Platform;
using Domain.Tenancy;
using Domain.Users;
using Domain.Webhooks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Tenancy;

/// <summary>
/// RN-013 (A1.4): the purge deletes the data of ONE Locked tenant whose purge date has passed, and nothing else.
/// </summary>
public class PurgeTenantCommandHandlerTests
{
    private static readonly DateTime CancelledAt = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Due = CancelledAt.AddDays(90);
    private static readonly DateTime Now = Due.AddDays(1);

    private readonly Guid _dealerId = Guid.NewGuid();
    private readonly Guid _otherId = Guid.NewGuid();
    private readonly Mock<IStorageService> _storage = new();
    private readonly Mock<ITenantLifecycleCache> _cache = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly TestApplicationDbContext _context;

    public PurgeTenantCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options, _dealerId);
    }

    private PurgeTenantCommandHandler Handler() =>
        new(_context, _storage.Object, _clock.Object, _cache.Object, NullLogger<PurgeTenantCommandHandler>.Instance);

    private Domain.DealerSettings.DealerSettings SeedDealer(Guid id, TenantLifecycleStatus target, string name = "Autos Sur SA")
    {
        var dealer = new Domain.DealerSettings.DealerSettings(id, name, $"{id:N}@dealer.com", address: "Calle 1", phoneNumber: "111");
        if (target is not TenantLifecycleStatus.Active)
        {
            dealer.StartRetention(CancelledAt, 90);
            if (target is TenantLifecycleStatus.Locked)
                dealer.LockAfterGrace(CancelledAt.AddDays(31), 30);
        }

        _context.DealerSettings.Add(dealer);
        _context.SaveChanges();
        return dealer;
    }

    private async Task<Car> SeedCarAsync(Guid dealerId, string patente, string? imageKey)
    {
        var marca = new Marca($"Marca-{patente}");
        var modelo = new Modelo($"Modelo-{patente}", marca.Id);
        _context.Marca.Add(marca);
        _context.Modelo.Add(modelo);
        var car = new Car(
            dealerId, marca, modelo, Color.Black, TypeCar.Hatchback, StatusCar.Used, StatusServiceCar.Disponible,
            5, 5, 1400, 20000, 2022, patente, "desc", 11000m, DateTime.UtcNow);
        _context.Cars.Add(car);
        if (imageKey is not null)
            _context.CarImages.Add(CarImage.Create(Guid.NewGuid(), car.Id, imageKey, "image/jpeg", 10, 0, true));
        await _context.SaveChangesAsync();
        return car;
    }

    private async Task SeedTenantDataAsync(Guid dealerId, string tag)
    {
        _context.Clients.Add(new Client(dealerId, "Ana", "Perez", $"dni-{tag}", $"{tag}@x.com", "111", "Street", Now));
        _context.Leads.Add(Lead.Create(dealerId, "Lead " + tag, $"l{tag}@x.com", "222", LeadSource.Web, Now));
        _context.Users.Add(new User(dealerId, $"user-{tag}@dealer.com", "U", "S", "hash", Guid.NewGuid()));
        _context.Documents.Add(Document.Create(null, DocumentType.DNI, $"docs/{tag}.png", "dni.png", "image/png", dealerId));
        _context.WebhookSubscriptions.Add(WebhookSubscription.Create(
            dealerId, "https://example.com/hook", new string('s', 32), new[] { "sale.created" }, Now));
        await SeedCarAsync(dealerId, $"{tag}{tag}{tag}111".ToUpperInvariant(), $"cars/{dealerId}/{tag}.jpg");
        await _context.SaveChangesAsync();
    }

    private async Task SeedRetainedDataAsync(Guid dealerId)
    {
        _context.DealerSubscriptions.Add(DealerSubscription.Create(dealerId, "cus_1", "sub_1", "plan"));
        _context.LegalAcceptances.Add(LegalAcceptance.Create(
            dealerId, Guid.NewGuid(), LegalDocumentKind.Terms, "2026-01-01", new string('a', 64), Now, "1.1.1.1", "ua"));
        _context.PlatformAuditLogs.Add(PlatformAuditLogEntry.Record(
            dealerId, dealerId, "Autos Sur SA", PlatformAuditAction.DealerProvisioned, Guid.NewGuid(), "a@a.com",
            PlatformAuditActorKind.SuperAdmin, Now, Now, $"evt-{dealerId}"));
        _context.ProcessedStripeEvents.Add(new ProcessedStripeEvent { StripeEventId = $"evt_{dealerId:N}", ProcessedOnUtc = Now, DealerId = dealerId });
        await _context.SaveChangesAsync();
    }

    private async Task<int> RowCountAsync(Guid dealerId) =>
        await _context.Cars.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await _context.Clients.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await _context.Leads.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await _context.Users.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await _context.Documents.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await _context.WebhookSubscriptions.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId);

    // ---------------------------------------------------------------- eligibility: anything else is a no-op

    [Theory]
    [InlineData(TenantLifecycleStatus.Active)]
    [InlineData(TenantLifecycleStatus.CancelledGrace)]
    public async Task Purge_OnTenantThatIsNotLocked_IsNoOp(TenantLifecycleStatus status)
    {
        SeedDealer(_dealerId, status);
        await SeedTenantDataAsync(_dealerId, "a");
        int before = await RowCountAsync(_dealerId);

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TenantPurgeErrors.NotEligible);
        (await RowCountAsync(_dealerId)).Should().Be(before);
        (await _context.DealerSettings.SingleAsync()).LifecycleStatus.Should().Be(status);
        (await _context.TenantPurgeRecords.CountAsync()).Should().Be(0);
        _storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.Invalidate(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Purge_OnLockedTenantNotYetDue_IsNoOp()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Locked);
        await SeedTenantDataAsync(_dealerId, "a");
        int before = await RowCountAsync(_dealerId);
        _clock.SetupGet(c => c.UtcNow).Returns(Due.AddMinutes(-1));

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        result.Error.Should().Be(TenantPurgeErrors.NotEligible);
        (await RowCountAsync(_dealerId)).Should().Be(before);
        (await _context.DealerSettings.SingleAsync()).LifecycleStatus.Should().Be(TenantLifecycleStatus.Locked);
        _storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Purge_OnAlreadyPurgedTenant_IsNoOp()
    {
        var dealer = SeedDealer(_dealerId, TenantLifecycleStatus.Locked);
        dealer.Purge(Now);
        await _context.SaveChangesAsync();

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        result.Error.Should().Be(TenantPurgeErrors.NotEligible);
        (await _context.TenantPurgeRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Purge_OnUnknownDealer_ReturnsNotFound()
    {
        var result = await Handler().Handle(new PurgeTenantCommand(Guid.NewGuid(), DryRun: false), CancellationToken.None);

        result.Error.Should().Be(TenantPurgeErrors.NotFound);
    }

    // ---------------------------------------------------------------- scope: one dealer only

    [Fact]
    public async Task Purge_RemovesOnlyTheTargetDealersRows()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Locked);
        SeedDealer(_otherId, TenantLifecycleStatus.Locked);
        await SeedTenantDataAsync(_dealerId, "a");
        await SeedTenantDataAsync(_otherId, "b");
        int otherBefore = await RowCountAsync(_otherId);
        otherBefore.Should().BeGreaterThan(0);

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await RowCountAsync(_dealerId)).Should().Be(0);
        (await RowCountAsync(_otherId)).Should().Be(otherBefore);
        (await _context.CarImages.CountAsync()).Should().Be(1, "only the other dealer's image survives");
        _storage.Verify(s => s.DeleteFileAsync($"cars/{_dealerId}/a.jpg", It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.DeleteFileAsync("docs/a.png", It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.DeleteFileAsync($"cars/{_otherId}/b.jpg", It.IsAny<CancellationToken>()), Times.Never);
        _storage.Verify(s => s.DeleteFileAsync("docs/b.png", It.IsAny<CancellationToken>()), Times.Never);
        (await _context.DealerSettings.IgnoreQueryFilters().SingleAsync(d => d.DealerId == _otherId))
            .LifecycleStatus.Should().Be(TenantLifecycleStatus.Locked);
    }

    // ---------------------------------------------------------------- what is kept

    [Fact]
    public async Task Purge_KeepsSubscriptionAuditAndLegalEvidence()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Locked);
        await SeedTenantDataAsync(_dealerId, "a");
        await SeedRetainedDataAsync(_dealerId);

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _context.DealerSubscriptions.IgnoreQueryFilters().CountAsync(x => x.DealerId == _dealerId)).Should().Be(1);
        (await _context.LegalAcceptances.IgnoreQueryFilters().CountAsync(x => x.DealerId == _dealerId)).Should().Be(1);
        (await _context.PlatformAuditLogs.IgnoreQueryFilters().CountAsync(x => x.DealerId == _dealerId)).Should().Be(1);
        (await _context.ProcessedStripeEvents.CountAsync(x => x.DealerId == _dealerId)).Should().Be(1);
    }

    [Fact]
    public async Task Purge_AnonymizesDealerSettingsAndWritesTombstoneWithCounts()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Locked, "Autos Sur SA");
        await SeedTenantDataAsync(_dealerId, "a");

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dealer = await _context.DealerSettings.IgnoreQueryFilters().SingleAsync();
        dealer.LifecycleStatus.Should().Be(TenantLifecycleStatus.Purged);
        dealer.DealerName.Should().Be("Autos Sur SA");
        dealer.Address.Should().BeNull();
        dealer.ContactEmail.Should().NotContain(_dealerId.ToString("N") + "@dealer.com");

        var record = await _context.TenantPurgeRecords.SingleAsync();
        record.DealerId.Should().Be(_dealerId);
        record.PurgedAtUtc.Should().Be(Now);
        record.Counts["cars"].Should().Be(1);
        record.Counts["clients"].Should().Be(1);
        record.Counts["leads"].Should().Be(1);
        record.Counts["users"].Should().Be(1);
        record.Counts["documents"].Should().Be(1);
        result.Value.Counts["cars"].Should().Be(1);
        _cache.Verify(c => c.Invalidate(_dealerId), Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------- dry run

    [Fact]
    public async Task DryRun_ReportsCountsButDeletesAndChangesNothing()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Locked);
        await SeedTenantDataAsync(_dealerId, "a");
        await SeedRetainedDataAsync(_dealerId);
        int before = await RowCountAsync(_dealerId);

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DryRun.Should().BeTrue();
        result.Value.Counts["cars"].Should().Be(1);
        result.Value.Counts["clients"].Should().Be(1);
        (await RowCountAsync(_dealerId)).Should().Be(before);
        (await _context.DealerSettings.SingleAsync()).LifecycleStatus.Should().Be(TenantLifecycleStatus.Locked);
        (await _context.DealerSubscriptions.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await _context.PlatformAuditLogs.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await _context.TenantPurgeRecords.CountAsync()).Should().Be(0);
        _storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.Invalidate(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DryRun_OnIneligibleTenant_IsAlsoRejected()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Active);

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: true), CancellationToken.None);

        result.Error.Should().Be(TenantPurgeErrors.NotEligible);
    }

    // ---------------------------------------------------------------- export files still downloadable

    [Fact]
    public async Task Purge_IsDeferredWhileAnExportIsStillDownloadable()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Locked);
        await SeedTenantDataAsync(_dealerId, "a");
        var export = TenantExport.Request(_dealerId, Guid.NewGuid(), Now.AddDays(-2));
        export.MarkRunning(Now.AddDays(-2));
        export.MarkReady($"exports/{_dealerId}/{export.Id}.zip", new string('a', 64), 10, Now.AddDays(-2)); // expires Now + 5 d
        _context.TenantExports.Add(export);
        await _context.SaveChangesAsync();
        int before = await RowCountAsync(_dealerId);

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        result.Error.Should().Be(TenantPurgeErrors.ExportStillAvailable);
        (await RowCountAsync(_dealerId)).Should().Be(before);
        _storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await _context.TenantExports.SingleAsync()).Status.Should().Be(TenantExportStatus.Ready);
    }

    [Fact]
    public async Task Purge_ProceedsOnceTheExportHasExpired_AndRemovesItsRow()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Locked);
        await SeedTenantDataAsync(_dealerId, "a");
        var export = TenantExport.Request(_dealerId, Guid.NewGuid(), Now.AddDays(-20));
        export.MarkRunning(Now.AddDays(-20));
        export.MarkReady($"exports/{_dealerId}/{export.Id}.zip", new string('a', 64), 10, Now.AddDays(-20));
        export.Expire(Now.AddDays(-13));
        _context.TenantExports.Add(export);
        await _context.SaveChangesAsync();

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _context.TenantExports.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Purge_IsDeferredWhileAnExportIsStillBeingBuilt()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Locked);
        _context.TenantExports.Add(TenantExport.Request(_dealerId, Guid.NewGuid(), Now.AddHours(-1)));
        await _context.SaveChangesAsync();

        var result = await Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        result.Error.Should().Be(TenantPurgeErrors.ExportStillAvailable);
    }

    // ---------------------------------------------------------------- failure safety

    [Fact]
    public async Task Purge_WhenStorageFails_KeepsTheRowsAndTheTenantLockedSoItCanBeRetried()
    {
        SeedDealer(_dealerId, TenantLifecycleStatus.Locked);
        await SeedTenantDataAsync(_dealerId, "a");
        _storage
            .Setup(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage down"));

        Func<Task> act = () => Handler().Handle(new PurgeTenantCommand(_dealerId, DryRun: false), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // Rows that own a stored file are only removed after the file is gone, so none of them was dropped.
        (await _context.Documents.CountAsync()).Should().Be(1);
        (await _context.CarImages.CountAsync()).Should().Be(1);
        (await _context.Cars.CountAsync()).Should().Be(1);
        (await _context.Clients.CountAsync()).Should().Be(1);
        (await _context.DealerSettings.SingleAsync()).LifecycleStatus.Should().Be(TenantLifecycleStatus.Locked);
        (await _context.TenantPurgeRecords.CountAsync()).Should().Be(0);
    }
}
