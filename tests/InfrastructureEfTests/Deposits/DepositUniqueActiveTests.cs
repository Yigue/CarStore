using Application.Abstractions.Tenancy;
using Domain.Cars.Attributes;
using Domain.Currency;
using Domain.Deposits;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfrastructureEfTests.Deposits;

/// <summary>
/// sfc B1.3.2 / RN-004: at most one Active deposit per car, enforced by the partial unique index
/// <c>ux_deposits_one_active_per_car</c> so two concurrent requests cannot both win. Inactive deposits do not count.
/// Runs on relational SQLite so the real index is exercised.
/// </summary>
public class DepositUniqueActiveTests
{
    private static readonly Guid TenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");
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

    // The aggregate's own guard is bypassed on purpose (HasActiveDeposit: false): the index is the last line.
    private static Deposit NewDeposit(Guid carId, ExchangeRateSnapshot? snapshot = null, string currency = "ARS") =>
        Deposit.Create(TenantId, carId, Guid.NewGuid(), null, 1000m, currency, snapshot, Now,
            new DepositCarContext(StatusServiceCar.Disponible, null, false, "ARS")).Value;

    [Fact]
    public async Task SecondActiveDeposit_OnTheSameCar_Should_BeRejectedByTheIndex()
    {
        await using var connection = await OpenSchemaAsync();
        var carId = Guid.NewGuid();
        await using (var first = NewContext(connection))
        {
            first.Deposits.Add(NewDeposit(carId));
            await first.SaveChangesAsync();
        }

        await using var second = NewContext(connection);
        second.Deposits.Add(NewDeposit(carId));
        Func<Task> act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ActiveDeposit_Next_To_An_Expired_One_Should_BeAllowed_AndRoundTrip()
    {
        await using var connection = await OpenSchemaAsync();
        var carId = Guid.NewGuid();
        var snapshot = new ExchangeRateSnapshot("USD", "ARS", 1100m, Now, Guid.NewGuid());
        Guid activeId;
        await using (var write = NewContext(connection))
        {
            var expired = NewDeposit(carId);
            expired.Expire(Now.AddDays(30)).IsSuccess.Should().BeTrue();
            var active = NewDeposit(carId, snapshot, "USD");
            write.Deposits.AddRange(expired, active);
            await write.SaveChangesAsync();
            activeId = active.Id;
        }

        await using var read = NewContext(connection);
        var loaded = await read.Deposits.AsNoTracking().SingleAsync(d => d.Id == activeId);
        loaded.Status.Should().Be(DepositStatus.Active);
        loaded.Amount.Currency.Should().Be("USD");
        loaded.ExchangeRate.Should().Be(snapshot);
        loaded.RefundedAmount.Should().BeNull();
    }
}
