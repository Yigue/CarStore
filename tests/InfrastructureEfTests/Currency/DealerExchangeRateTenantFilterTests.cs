using Application.Abstractions.Tenancy;
using Domain.Currency;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfrastructureEfTests.Currency;

/// <summary>
/// sfc B0.2 / security review: exchange rates are tenant data. Every read through ApplicationDbContext must be pinned to
/// the caller's dealer by the global query filter, not only by each handler remembering to add the predicate.
/// </summary>
public class DealerExchangeRateTenantFilterTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class TenantService(Guid dealerId) : ICurrentTenantService
    {
        public Guid DealerId => dealerId;
        public bool HasTenant => true;
    }

    private static async Task<(ApplicationDbContext Context, SqliteConnection Connection)> CreateContextAsync(Guid tenant)
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var context = new ApplicationDbContext(options, new NoOpPublisher(), new TenantService(tenant));
        await context.Database.EnsureCreatedAsync();
        return (context, connection);
    }

    [Fact]
    public async Task Reads_Should_OnlyReturnTheCurrentTenantsRates()
    {
        var (seed, seedConnection) = await CreateContextAsync(TenantA);
        await using var _seed = seed;
        using var __ = seedConnection;

        seed.DealerExchangeRates.Add(DealerExchangeRate.Record(TenantA, CurrencyCode.Usd, CurrencyCode.Ars, 1000m, T0, TenantA).Value);
        seed.DealerExchangeRates.Add(DealerExchangeRate.Record(TenantB, CurrencyCode.Usd, CurrencyCode.Ars, 2000m, T0, TenantB).Value);
        await seed.SaveChangesAsync();

        // Same database, now read as tenant A through the global filter (no explicit DealerId predicate).
        var asTenantA = await seed.DealerExchangeRates.AsNoTracking().ToListAsync();

        asTenantA.Should().ContainSingle().Which.Rate.Should().Be(1000m);
    }
}
