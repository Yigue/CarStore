using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Domain.Cars.Events;
using Domain.Shared;
using FluentAssertions;
using Infrastructure.BackgroundJobs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Quartz;
using Xunit;

namespace WebApiTests.Postgres;

/// <summary>
/// spr PR 5.1 (OPS-07): defense-in-depth regression for the atomic claim added to
/// <see cref="ProcessOutboxMessagesJob"/> (<c>UPDATE ... RETURNING</c> guarded by
/// <c>FOR UPDATE SKIP LOCKED</c>). Two separate hosts — two independent
/// <see cref="PostgresWebApplicationFactory"/> instances, i.e. two independent DI
/// containers/connections — race <see cref="ProcessOutboxMessagesJob.Execute"/> concurrently
/// against one pre-seeded outbox message on the SAME real Postgres (Testcontainers). Without the
/// claim, both hosts would read the same unprocessed row and publish it twice; with the claim,
/// exactly one host wins the row and the message is published exactly once, by exactly one host.
///
/// Scope decision (documented, not silently assumed): this exercises the claim directly by
/// invoking the job's <c>Execute</c> on two hosts concurrently, rather than booting two full
/// Quartz-clustered schedulers and waiting for their real timers to both fire the 10s trigger.
/// The timer-based variant depends on Quartz's internal cluster check-in/trigger-acquisition
/// timing (several seconds of wall-clock, inherently racy to assert on deterministically in CI)
/// to even reach the exact point this test already exercises directly and deterministically.
/// Quartz's own clustering guarantee (only one node fires a given trigger cluster-wide) is a
/// separate, already-reflected-and-confirmed library behavior (see
/// docs/runbooks/quartz-clustering-y-multi-instancia.md) — this test covers the independent
/// "defense in depth" layer that protects even the brief blue/green overlap window where two
/// schedulers are briefly alive before clustering's own trigger exclusivity converges.
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class OutboxClusteredClaimPostgresTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _hostA;
    private readonly PostgresWebApplicationFactory _hostB;

    public OutboxClusteredClaimPostgresTests(PostgresFixture fixture)
    {
        _hostA = new PostgresWebApplicationFactory(fixture.GetConnectionString());
        _hostB = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        // Host A applies migrations (including AddQuartzClusteredTables) and seeds the shared
        // database. Host B is a second, independent DI container/connection against the same
        // already-migrated database — standing in for a second live process.
        await _hostA.InitializeDatabaseAsync();
        using var scopeB = _hostB.Services.CreateScope();
        _ = scopeB.ServiceProvider.GetRequiredService<IApplicationDbContext>();
    }

    public async Task DisposeAsync()
    {
        await _hostA.DisposeAsync();
        await _hostB.DisposeAsync();
    }

    [Fact]
    public async Task TwoHosts_RaceProcessOutboxMessagesJob_MessageIsPublishedExactlyOnce()
    {
        Guid carId = Guid.NewGuid();
        Guid messageId = Guid.NewGuid();

        using (var seedScope = _hostA.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            seedContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = messageId,
                Type = nameof(CarDeleteDomainEvent),
                Content = JsonConvert.SerializeObject(new CarDeleteDomainEvent(carId)),
                OccurredOnUtc = DateTime.UtcNow,
                ProcessedOnUtc = null
            });
            await seedContext.SaveChangesAsync();
        }

        var publisherA = new CountingPublisher();
        var publisherB = new CountingPublisher();

        using var scopeA = _hostA.Services.CreateScope();
        using var scopeB = _hostB.Services.CreateScope();

        var jobA = new ProcessOutboxMessagesJob(
            scopeA.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            scopeA.ServiceProvider.GetRequiredService<DbContext>(),
            publisherA,
            NullLogger<ProcessOutboxMessagesJob>.Instance);

        var jobB = new ProcessOutboxMessagesJob(
            scopeB.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            scopeB.ServiceProvider.GetRequiredService<DbContext>(),
            publisherB,
            NullLogger<ProcessOutboxMessagesJob>.Instance);

        var contextA = new FakeJobExecutionContext(CancellationToken.None);
        var contextB = new FakeJobExecutionContext(CancellationToken.None);

        await Task.WhenAll(jobA.Execute(contextA), jobB.Execute(contextB));

        int totalPublishes = publisherA.PublishCount + publisherB.PublishCount;
        totalPublishes.Should().Be(1, "exactly one host must win the atomic claim and publish the message exactly once");

        using var assertScope = _hostA.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        OutboxMessage? persisted = await assertContext.OutboxMessages.FindAsync(messageId);
        persisted.Should().NotBeNull();
        persisted!.ProcessedOnUtc.Should().NotBeNull("the winning host must mark the message processed");
    }

    private sealed class CountingPublisher : IPublisher
    {
        private int _publishCount;

        public int PublishCount => _publishCount;

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _publishCount);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Interlocked.Increment(ref _publishCount);
            return Task.CompletedTask;
        }
    }

    /// <summary>Minimal <see cref="IJobExecutionContext"/> stand-in: ProcessOutboxMessagesJob
    /// only reads <see cref="CancellationToken"/> from it. Hand-rolled instead of a mocking
    /// library, since this test project has no Moq/NSubstitute reference.</summary>
    private sealed class FakeJobExecutionContext(CancellationToken cancellationToken) : IJobExecutionContext
    {
        public CancellationToken CancellationToken { get; } = cancellationToken;

        public object? Result { get; set; }

        public void Put(object key, object objectValue) => throw new NotSupportedException();
        public object Get(object key) => throw new NotSupportedException();

        public IScheduler Scheduler => throw new NotSupportedException();
        public ITrigger Trigger => throw new NotSupportedException();
        public ICalendar? Calendar => throw new NotSupportedException();
        public bool Recovering => false;
        public TriggerKey RecoveringTriggerKey => throw new NotSupportedException();
        public int RefireCount => 0;
        public JobDataMap MergedJobDataMap => new();
        public IJobDetail JobDetail => throw new NotSupportedException();
        public IJob JobInstance => throw new NotSupportedException();
        public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
        public DateTimeOffset? ScheduledFireTimeUtc => null;
        public DateTimeOffset? PreviousFireTimeUtc => null;
        public DateTimeOffset? NextFireTimeUtc => null;
        public string FireInstanceId => Guid.NewGuid().ToString();
        public TimeSpan JobRunTime => TimeSpan.Zero;
    }
}
