using System;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Domain.Billing;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace WebApiTests.Postgres;

/// <summary>
/// spr ADR B8 / task 3.1.12: the fail-closed gate answers 402 to a dealer without a subscription row,
/// so the migration that ships with it must give every existing dealer one FIRST. Runs the real
/// migrations on a throwaway database: schema up to the previous migration, an existing dealer set,
/// then the backfill migration.
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class SubscriptionBackfillMigrationPostgresTests(PostgresFixture fixture)
{
    private const string PreviousMigration = "20261010152035_AddMoneyCurrencyColumns";

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, System.Threading.CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class NoTenant : ICurrentTenantService
    {
        public Guid DealerId => Guid.Empty;
        public bool HasTenant => false;
    }

    private static ApplicationDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .Options,
            new NoOpPublisher(),
            new NoTenant());

    [Fact]
    public async Task Backfill_CreatesIncompleteRow_ForDealersWithoutOne_AndLeavesExistingRowsAlone()
    {
        string database = $"backfill_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(fixture.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{database}\"";
            await create.ExecuteNonQueryAsync();
        }

        string connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            Database = database
        }.ConnectionString;

        var withoutRow = Guid.NewGuid();
        var withActiveRow = Guid.NewGuid();
        var createdAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

        await using (var before = NewContext(connectionString))
        {
            // Schema as it was before the fail-closed release.
            await before.GetService<IMigrator>().MigrateAsync(PreviousMigration);

            var legacy = new DealerSettingsEntity(withoutRow, withoutRow, "Legacy", "legacy@x.com", true, "legacy-dealer");
            legacy.SetStripeCustomerId("cus_legacy");
            typeof(DealerSettingsEntity).GetProperty(nameof(DealerSettingsEntity.CreatedAt))!.SetValue(legacy, createdAt);
            before.DealerSettings.Add(legacy);

            before.DealerSettings.Add(new DealerSettingsEntity(withActiveRow, withActiveRow, "Paying", "paying@x.com", true, "paying-dealer"));
            var active = DealerSubscription.Create(withActiveRow, "cus_paying", "sub_paying", "price_1");
            active.Activate("sub_paying", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(29));
            before.DealerSubscriptions.Add(active);
            await before.SaveChangesAsync();
        }

        await using (var after = NewContext(connectionString))
        {
            await after.Database.MigrateAsync();

            var rows = await after.DealerSubscriptions.IgnoreQueryFilters().ToListAsync();

            var created = rows.Should().ContainSingle(r => r.DealerId == withoutRow).Subject;
            created.Status.Should().Be(SubscriptionStatus.Incomplete);
            created.StripeCustomerId.Should().Be("cus_legacy");
            created.StripeSubscriptionId.Should().BeNull();
            created.CurrentPeriodStart.Should().Be(createdAt, "the grace for reconciliation counts from when the dealer appeared");

            var untouched = rows.Should().ContainSingle(r => r.DealerId == withActiveRow).Subject;
            untouched.Status.Should().Be(SubscriptionStatus.Active);
            untouched.StripeSubscriptionId.Should().Be("sub_paying");

            // Idempotent: a second pass over the same data inserts nothing.
            await after.Database.ExecuteSqlRawAsync(@"
INSERT INTO public.dealer_subscriptions (id, dealer_id, status, current_period_start, current_period_end, plan_id)
SELECT gen_random_uuid(), ds.dealer_id, 'Incomplete', ds.created_at, ds.created_at, ''
FROM public.dealer_settings ds
WHERE NOT EXISTS (SELECT 1 FROM public.dealer_subscriptions s WHERE s.dealer_id = ds.dealer_id);");
            (await after.DealerSubscriptions.IgnoreQueryFilters().CountAsync()).Should().Be(2);
        }
    }
}
