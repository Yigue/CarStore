using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Domain.Tenancy;
using FluentAssertions;
using Infrastructure.BackgroundJobs;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;

namespace InfrastructureEfTests.Tenancy;

/// <summary>
/// Follow-up to A1.4: the retry job drains PendingStorageDeletions. Success (or an already-missing object, which
/// IStorageService.DeleteFileAsync reports as success by contract) removes the row; failures bump Attempts and keep it;
/// rows at the maximum are never retried but stay, with an Error log, for a human to review.
/// </summary>
public class PendingStorageDeletionRetryJobTests
{
    private static readonly Guid Dealer = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTime T0 = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

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

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
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

    private static PendingStorageDeletion Row(string key, int minutes = 0, int extraFailures = 0)
    {
        var row = PendingStorageDeletion.Create(Dealer, key, T0.AddMinutes(minutes), "first failure");
        for (int i = 0; i < extraFailures; i++)
            row.RecordFailure("again");
        return row;
    }

    private static async Task<(SqliteConnection Connection, ApplicationDbContext Db)> SeedAsync(params PendingStorageDeletion[] rows)
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        ApplicationDbContext db = NewContext(connection);
        await db.Database.EnsureCreatedAsync();
        db.PendingStorageDeletions.AddRange(rows);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (connection, db);
    }

    private static async Task RunAsync(ApplicationDbContext db, Mock<IStorageService> storage, ILogger<StorageDeletionRetryJob> logger)
    {
        var job = new StorageDeletionRetryJob(db, storage.Object, logger);
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(context.Object);
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task HealthyStorage_DeletesTheObjectAndRemovesTheRow()
    {
        (SqliteConnection connection, ApplicationDbContext db) = await SeedAsync(Row("cars/a.jpg"));
        await using var _ = connection; await using var __ = db;
        var storage = new Mock<IStorageService>();

        await RunAsync(db, storage, new ListLogger<StorageDeletionRetryJob>());

        storage.Verify(s => s.DeleteFileAsync("cars/a.jpg", It.IsAny<CancellationToken>()), Times.Once);
        (await db.PendingStorageDeletions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TransientError_KeepsTheRowAndRecordsTheAttempt()
    {
        (SqliteConnection connection, ApplicationDbContext db) = await SeedAsync(Row("cars/a.jpg"));
        await using var _ = connection; await using var __ = db;
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("still down"));

        await RunAsync(db, storage, new ListLogger<StorageDeletionRetryJob>());

        PendingStorageDeletion row = await db.PendingStorageDeletions.SingleAsync();
        row.Attempts.Should().Be(2);
        row.LastError.Should().Be("still down");
    }

    [Fact]
    public async Task LongError_IsTruncatedToTheColumnLength()
    {
        (SqliteConnection connection, ApplicationDbContext db) = await SeedAsync(Row("cars/a.jpg"));
        await using var _ = connection; await using var __ = db;
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(new string('x', 5000)));

        await RunAsync(db, storage, new ListLogger<StorageDeletionRetryJob>());

        (await db.PendingStorageDeletions.SingleAsync()).LastError!.Length.Should().Be(PendingStorageDeletion.MaxErrorLength);
    }

    [Fact]
    public async Task ObjectAlreadyMissing_CountsAsSuccessAndRemovesTheRow()
    {
        // Contract of IStorageService.DeleteFileAsync: a missing object completes normally (Minio swallows NoSuchKey).
        (SqliteConnection connection, ApplicationDbContext db) = await SeedAsync(Row("cars/gone.jpg"));
        await using var _ = connection; await using var __ = db;
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.DeleteFileAsync("cars/gone.jpg", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await RunAsync(db, storage, new ListLogger<StorageDeletionRetryJob>());

        (await db.PendingStorageDeletions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RowAtMaxAttempts_IsNotRetriedStaysAndLogsAnError()
    {
        (SqliteConnection connection, ApplicationDbContext db) =
            await SeedAsync(Row("cars/stuck.jpg", extraFailures: PendingStorageDeletion.MaxAttempts - 1));
        await using var _ = connection; await using var __ = db;
        var storage = new Mock<IStorageService>();
        var logger = new ListLogger<StorageDeletionRetryJob>();

        await RunAsync(db, storage, logger);

        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        PendingStorageDeletion row = await db.PendingStorageDeletions.SingleAsync();
        row.Attempts.Should().Be(PendingStorageDeletion.MaxAttempts);
        logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Error && e.Message.Contains("cars/stuck.jpg") && e.Message.Contains(Dealer.ToString()));
    }

    [Fact]
    public async Task OneFailingKey_DoesNotStopTheRestOfTheBatch()
    {
        (SqliteConnection connection, ApplicationDbContext db) =
            await SeedAsync(Row("k/1", 1), Row("k/bad", 2), Row("k/3", 3));
        await using var _ = connection; await using var __ = db;
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.DeleteFileAsync("k/bad", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await RunAsync(db, storage, new ListLogger<StorageDeletionRetryJob>());

        storage.Verify(s => s.DeleteFileAsync("k/3", It.IsAny<CancellationToken>()), Times.Once);
        List<PendingStorageDeletion> left = await db.PendingStorageDeletions.ToListAsync();
        left.Should().ContainSingle().Which.ObjectKey.Should().Be("k/bad");
        left[0].Attempts.Should().Be(2);
    }

    [Fact]
    public async Task Batch_IsBoundedAndOldestFirst()
    {
        PendingStorageDeletion[] rows = Enumerable.Range(0, StorageDeletionRetryJob.BatchSize + 5)
            .Select(i => Row($"k/{i}", i)).ToArray();
        (SqliteConnection connection, ApplicationDbContext db) = await SeedAsync(rows);
        await using var _ = connection; await using var __ = db;
        var storage = new Mock<IStorageService>();

        await RunAsync(db, storage, new ListLogger<StorageDeletionRetryJob>());

        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(StorageDeletionRetryJob.BatchSize));
        (await db.PendingStorageDeletions.Select(p => p.ObjectKey).ToListAsync())
            .Should().BeEquivalentTo(Enumerable.Range(StorageDeletionRetryJob.BatchSize, 5).Select(i => $"k/{i}"));
    }

    [Fact]
    public async Task ExhaustedRows_DoNotStarveTheBatch()
    {
        var stuck = Enumerable.Range(0, 3).Select(i => Row($"stuck/{i}", i, PendingStorageDeletion.MaxAttempts - 1)).ToList();
        PendingStorageDeletion fresh = Row("fresh/1", 10);
        (SqliteConnection connection, ApplicationDbContext db) = await SeedAsync([.. stuck, fresh]);
        await using var _ = connection; await using var __ = db;
        var storage = new Mock<IStorageService>();

        await RunAsync(db, storage, new ListLogger<StorageDeletionRetryJob>());

        storage.Verify(s => s.DeleteFileAsync("fresh/1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
