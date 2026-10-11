using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Tenancy.Purge;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.DealerSettings;
using Domain.Documents;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Sales;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Domain.Webhooks;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace InfrastructureEfTests.Tenancy;

/// <summary>
/// RN-013 (A1.4): runs the real purge against SQLite with foreign keys ENFORCED (the schema comes from the EF model,
/// the same FKs Postgres has). Proves the delete order works, including the Client/Lead cycle, and that a second
/// dealer's rows survive untouched.
/// </summary>
public class PurgeTenantForeignKeyOrderTests
{
    private static readonly Guid DealerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DealerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTime Cancelled = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Cancelled.AddDays(91);

    private sealed class NoTenant : ICurrentTenantService
    {
        public Guid DealerId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class RecordingStorage : IStorageService
    {
        public List<string> Deleted { get; } = [];
        public Task<string> UploadFileAsync(Stream stream, string objectKey, string contentType, long? size, CancellationToken ct) => Task.FromResult(objectKey);
        public Task DeleteFileAsync(string objectKey, CancellationToken ct) { Deleted.Add(objectKey); return Task.CompletedTask; }
        public Task<Uri> GetPresignedUrlAsync(string objectKey, TimeSpan ttl, CancellationToken ct) => Task.FromResult(new Uri("https://x.test"));
        public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken ct) => Task.FromResult<Stream?>(null);
        public Task<(string Url, IReadOnlyDictionary<string, string> Fields)> GeneratePresignedPostAsync(string objectKey, string contentType, TimeSpan ttl, CancellationToken ct) =>
            Task.FromResult<(string, IReadOnlyDictionary<string, string>)>(("", new Dictionary<string, string>()));
    }

    private sealed class FailingStorage : IStorageService
    {
        public Task<string> UploadFileAsync(Stream stream, string objectKey, string contentType, long? size, CancellationToken ct) => Task.FromResult(objectKey);
        public Task DeleteFileAsync(string objectKey, CancellationToken ct) => throw new InvalidOperationException("storage down");
        public Task<Uri> GetPresignedUrlAsync(string objectKey, TimeSpan ttl, CancellationToken ct) => Task.FromResult(new Uri("https://x.test"));
        public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken ct) => Task.FromResult<Stream?>(null);
        public Task<(string Url, IReadOnlyDictionary<string, string> Fields)> GeneratePresignedPostAsync(string objectKey, string contentType, TimeSpan ttl, CancellationToken ct) =>
            Task.FromResult<(string, IReadOnlyDictionary<string, string>)>(("", new Dictionary<string, string>()));
    }

    private sealed class NoOpCache : ITenantLifecycleCache
    {
        public List<Guid> Invalidated { get; } = [];
        public void Invalidate(Guid dealerId) => Invalidated.Add(dealerId);
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime UtcNow => Now;
    }

    private static ApplicationDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new ApplicationDbContext(options, new NoOpPublisher(), new NoTenant());
    }

    private static async Task SeedGraphAsync(ApplicationDbContext db, Guid dealerId, string tag)
    {
        var dealer = new DealerSettingsEntity(dealerId, $"Dealer {tag}", $"{tag}@dealer.com");
        dealer.StartRetention(Cancelled, 90);
        dealer.LockAfterGrace(Cancelled.AddDays(31), 30);
        db.DealerSettings.Add(dealer);

        var marca = new Marca($"Marca-{tag}");
        var modelo = new Modelo($"Modelo-{tag}", marca.Id);
        db.Marca.Add(marca);
        db.Modelo.Add(modelo);
        var car = new Car(
            dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 1600, 90_000, 2020, $"{tag}{tag}{tag}111".ToUpperInvariant(), "car", 10_000m,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), FuelType.Gasolina, false, Transmission.Manual, null);
        db.Cars.Add(car);
        db.CarImages.Add(CarImage.Create(Guid.NewGuid(), car.Id, $"cars/{dealerId}/img.jpg", "image/jpeg", 10, 0, true));

        var client = new Client(dealerId, "Ana", "Perez", $"dni-{tag}", $"{tag}@x.com", "111", "Calle 1", Now);
        var lead = Lead.Create(dealerId, "Lead", $"l{tag}@x.com", "222", LeadSource.Web, Now, car.Id);
        db.Clients.Add(client);
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        // The cycle: clients.origin_lead_id -> leads and leads.converted_client_id -> clients.
        client.LinkOriginLead(lead.Id);
        lead.MarkConverted(client.Id);
        await db.SaveChangesAsync();

        var quote = new Quote(dealerId, car, client, lead, 1m, Domain.Quotes.Attributes.PaymentMethod.Contado, Now.AddDays(5), "c", Now);
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();

        var sale = new Sale(dealerId, car.Id, client.Id, 1m, PaymentMethod.Cash, $"C-{tag}", "c", Now);
        db.Sales.Add(sale);
        var category = new TransactionCategory(dealerId, $"Ventas {tag}", "Ventas", TransactionType.Income);
        db.TransactionCategories.Add(category);
        await db.SaveChangesAsync();

        db.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Income, new Money(250m, "ARS"), "desc", PaymentMethod.Cash, category));
        db.Documents.Add(Document.Create(client.Id, DocumentType.DNI, $"docs/{dealerId}/dni.png", "dni.png", "image/png", dealerId, sale.Id));

        var role = new Role(dealerId, $"Admin-{tag}", "role");
        role.AddPermission("CanManageSettings");
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User(dealerId, $"u{tag}@dealer.com", "U", "S", "hash", role.Id);
        db.Users.Add(user);
        db.WebhookSubscriptions.Add(WebhookSubscription.Create(
            dealerId, "https://example.com/hook", new string('s', 32), new[] { "sale.created" }, Now));
        await db.SaveChangesAsync();
    }

    private static async Task<int> RowsOfAsync(ApplicationDbContext db, Guid dealerId) =>
        await db.Cars.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.Clients.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.Leads.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.Quotes.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.Sales.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.Transactions.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.TransactionCategories.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.Documents.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.Users.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.Roles.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId)
        + await db.WebhookSubscriptions.IgnoreQueryFilters().CountAsync(x => x.DealerId == dealerId);

    [Fact]
    public async Task Purge_WithEnforcedForeignKeys_RemovesOneDealerAndLeavesTheOtherIntact()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = NewContext(connection);
        await db.Database.EnsureCreatedAsync();
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys";
            Convert.ToInt32(await pragma.ExecuteScalarAsync()).Should().Be(1, "the test is only meaningful with FK enforcement on");
        }

        await SeedGraphAsync(db, DealerA, "a");
        await SeedGraphAsync(db, DealerB, "b");
        int otherBefore = await RowsOfAsync(db, DealerB);
        otherBefore.Should().BeGreaterThan(8);

        var storage = new RecordingStorage();
        var cache = new NoOpCache();
        var handler = new PurgeTenantCommandHandler(db, storage, new FixedClock(), cache, NullLogger<PurgeTenantCommandHandler>.Instance);

        Result<TenantPurgeResult> result = await handler.Handle(new PurgeTenantCommand(DealerA, DryRun: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : "");
        (await RowsOfAsync(db, DealerA)).Should().Be(0);
        (await RowsOfAsync(db, DealerB)).Should().Be(otherBefore);
        (await db.CarImages.CountAsync()).Should().Be(1);
        (await db.DealerSettings.IgnoreQueryFilters().SingleAsync(d => d.DealerId == DealerA)).LifecycleStatus
            .Should().Be(TenantLifecycleStatus.Purged);
        (await db.DealerSettings.IgnoreQueryFilters().SingleAsync(d => d.DealerId == DealerB)).LifecycleStatus
            .Should().Be(TenantLifecycleStatus.Locked);
        (await db.TenantPurgeRecords.SingleAsync()).DealerId.Should().Be(DealerA);
        storage.Deleted.Should().BeEquivalentTo(new[] { $"cars/{DealerA}/img.jpg", $"docs/{DealerA}/dni.png" });
        cache.Invalidated.Should().Contain(DealerA).And.NotContain(DealerB);
    }

    [Fact]
    public async Task Purge_WhenItFailsHalfWay_RollsBackEveryDeleteMadeBeforeTheFailure()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = NewContext(connection);
        await db.Database.EnsureCreatedAsync();
        await SeedGraphAsync(db, DealerA, "a");
        int before = await RowsOfAsync(db, DealerA);
        var handler = new PurgeTenantCommandHandler(db, new FailingStorage(), new FixedClock(), new NoOpCache(), NullLogger<PurgeTenantCommandHandler>.Instance);

        Func<Task> act = () => handler.Handle(new PurgeTenantCommand(DealerA, DryRun: false), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        db.ChangeTracker.Clear();
        (await RowsOfAsync(db, DealerA)).Should().Be(before, "one transaction: nothing deleted before the failure may survive");
        (await db.DealerSettings.IgnoreQueryFilters().SingleAsync(d => d.DealerId == DealerA)).LifecycleStatus
            .Should().Be(TenantLifecycleStatus.Locked);
    }
}
