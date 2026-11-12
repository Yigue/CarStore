using Application.Abstractions.Tenancy;
using Domain.Commissions;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfrastructureEfTests.Commissions;

/// <summary>
/// sfc B4.1 security review: the real last-resort guard is <c>ux_sale_commissions_sale_id</c>, exercised here
/// on relational SQLite. A lost race must not leave the losing <see cref="SaleCommission"/> tracked on the
/// shared <c>DbContext</c> — <c>ProcessOutboxMessagesJob</c> reuses one context across a whole batch and calls
/// <c>SaveChangesAsync</c> again, unconditionally, after every handler in the batch has run; a still-tracked
/// zombie entity would fail that trailing save too and poison every other message in the batch.
/// </summary>
public class SaleCommissionRaceTests
{
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

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

    private static SaleCommission NewCommission(Guid saleId) =>
        SaleCommission.Create(TenantId, saleId, Guid.NewGuid(),
            new CommissionResult(new Money(100_000m, "ARS"), null, new Money(0m, "ARS"), IsConfigured: false), Now);

    [Fact]
    public async Task SecondCommission_ForTheSameSale_ThrowsOnTheRealUniqueIndex()
    {
        await using var connection = await OpenSchemaAsync();
        var saleId = Guid.NewGuid();
        await using (var first = NewContext(connection))
        {
            first.SaleCommissions.Add(NewCommission(saleId));
            await first.SaveChangesAsync();
        }

        await using var second = NewContext(connection);
        second.SaleCommissions.Add(NewCommission(saleId));
        Func<Task> act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task AfterLosingTheRace_TheSameContextCanStillSaveAnUnrelatedChange_IfTheLoserIsDetached()
    {
        await using var connection = await OpenSchemaAsync();
        var saleId = Guid.NewGuid();
        await using (var first = NewContext(connection))
        {
            first.SaleCommissions.Add(NewCommission(saleId));
            await first.SaveChangesAsync();
        }

        await using var shared = NewContext(connection);
        var loser = NewCommission(saleId);
        shared.SaleCommissions.Add(loser);
        await Assert.ThrowsAsync<DbUpdateException>(() => shared.SaveChangesAsync());

        // Mirrors the handler's own catch: detach the loser so the shared context (same one
        // ProcessOutboxMessagesJob reuses for the rest of its batch) is clean again.
        shared.DetachEntity(loser);

        var otherSaleId = Guid.NewGuid();
        shared.SaleCommissions.Add(NewCommission(otherSaleId));
        Func<Task> act = () => shared.SaveChangesAsync();

        await act.Should().NotThrowAsync("an unrelated, legitimate insert on the same shared context must not be poisoned by the earlier loss");
    }
}
