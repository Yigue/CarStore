using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using FluentAssertions;
using Infrastructure.Common;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfrastructureEfTests.Quotes;

/// <summary>
/// sfc B4.3 / RN-018 / ADR C7: concurrent folio allocations for the same (dealer, kind) never
/// duplicate and never skip a value, enforced by the atomic
/// <c>INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING</c> statement in
/// <see cref="DealerSequenceAllocator"/> — run on relational SQLite (not InMemory) so the real
/// statement executes, backed by the unique <c>(dealer_id, kind)</c> index.
/// </summary>
public class DealerSequenceConcurrencyTests
{
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private sealed class FakeTenant : ICurrentTenantService
    {
        public Guid DealerId => TenantId;
        public bool HasTenant => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private static ApplicationDbContext NewContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                .Options,
            new NoOpPublisher(), new FakeTenant());

    private static async Task<SqliteConnection> OpenSchemaAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var ctx = NewContext(connection);
        await ctx.Database.EnsureCreatedAsync();
        return connection;
    }

    [Fact]
    public async Task ConcurrentAllocations_ForTheSameDealerAndKind_Should_NeverDuplicate_AndBeContiguous()
    {
        await using SqliteConnection connection = await OpenSchemaAsync();
        const int callers = 20;

        // Each caller gets its own DbContext/connection-sharing instance — the point of this test
        // is that the DB serializes them, not that a single context does.
        Task<long>[] tasks = Enumerable.Range(0, callers)
            .Select(async _ =>
            {
                await using ApplicationDbContext ctx = NewContext(connection);
                IDealerSequenceAllocator allocator = new DealerSequenceAllocator(ctx);
                return await allocator.NextAsync(TenantId, "quote", CancellationToken.None);
            })
            .ToArray();

        long[] results = await Task.WhenAll(tasks);

        results.Should().OnlyHaveUniqueItems();
        results.OrderBy(x => x).Should().BeEquivalentTo(Enumerable.Range(1, callers).Select(i => (long)i), options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task SequentialAllocations_ForDifferentKinds_Should_NotInterfere()
    {
        await using SqliteConnection connection = await OpenSchemaAsync();

        await using var ctx = NewContext(connection);
        IDealerSequenceAllocator allocator = new DealerSequenceAllocator(ctx);

        long firstQuote = await allocator.NextAsync(TenantId, "quote", CancellationToken.None);
        long firstInvoice = await allocator.NextAsync(TenantId, "invoice", CancellationToken.None);
        long secondQuote = await allocator.NextAsync(TenantId, "quote", CancellationToken.None);

        firstQuote.Should().Be(1);
        firstInvoice.Should().Be(1);
        secondQuote.Should().Be(2);
    }

    [Fact]
    public async Task SequentialAllocations_ForDifferentDealers_Should_NotInterfere()
    {
        await using SqliteConnection connection = await OpenSchemaAsync();
        var otherDealerId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        await using var ctx = NewContext(connection);
        IDealerSequenceAllocator allocator = new DealerSequenceAllocator(ctx);

        long first = await allocator.NextAsync(TenantId, "quote", CancellationToken.None);
        long firstOther = await allocator.NextAsync(otherDealerId, "quote", CancellationToken.None);
        long second = await allocator.NextAsync(TenantId, "quote", CancellationToken.None);

        first.Should().Be(1);
        firstOther.Should().Be(1);
        second.Should().Be(2);
    }
}
