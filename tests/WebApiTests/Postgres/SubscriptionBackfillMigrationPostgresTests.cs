using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Inserts one row using only the columns the table has at the current migration. Columns that
    /// are NOT NULL without a default and not supplied get a neutral value for their type, so the
    /// test keeps working when a later migration adds a required column to an OLD schema snapshot
    /// it never sees.
    /// </summary>
    private static async Task InsertAsync(ApplicationDbContext context, string table, Dictionary<string, object> values)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        var columns = new List<(string Name, string Type, bool Required)>();
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = @"
SELECT column_name, data_type, (is_nullable = 'NO' AND column_default IS NULL)
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = @table";
            schema.Parameters.AddWithValue("table", table);
            await using var reader = await schema.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add((reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)));
            }
        }

        columns.Should().NotBeEmpty($"table {table} must exist at this migration");
        foreach (string supplied in values.Keys)
        {
            columns.Should().Contain(c => c.Name == supplied, $"column {table}.{supplied} must exist at this migration");
        }

        foreach (var column in columns.Where(c => c.Required && !values.ContainsKey(c.Name)))
        {
            values[column.Name] = column.Type switch
            {
                "uuid" => Guid.NewGuid(),
                "boolean" => false,
                "integer" or "bigint" or "smallint" => 0,
                "numeric" or "double precision" or "real" => 0m,
                "timestamp with time zone" or "timestamp without time zone" => DateTime.UtcNow,
                _ => string.Empty,
            };
        }

        string[] names = values.Keys.ToArray();
        await using var insert = connection.CreateCommand();
        insert.CommandText =
            $"INSERT INTO public.\"{table}\" ({string.Join(", ", names.Select(n => $"\"{n}\""))}) " +
            $"VALUES ({string.Join(", ", names.Select((_, i) => $"@p{i}"))})";
        for (int i = 0; i < names.Length; i++)
        {
            insert.Parameters.AddWithValue($"p{i}", values[names[i]]);
        }

        await insert.ExecuteNonQueryAsync();
    }

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

            // Raw SQL on purpose. This schema is the one from before the fail-closed release, and
            // the EF model is today's: saving entities through it emits every column the model has
            // gained since (billing_email was the first to break it), which do not exist yet at
            // PreviousMigration. The helper fills whatever else that old schema requires.
            await InsertAsync(before, "dealer_settings", new()
            {
                ["id"] = Guid.NewGuid(),
                ["dealer_id"] = withoutRow,
                ["dealer_name"] = "Legacy",
                ["contact_email"] = "legacy@x.com",
                ["is_active"] = true,
                ["slug"] = "legacy-dealer",
                ["stripe_customer_id"] = "cus_legacy",
                ["created_at"] = createdAt,
            });
            await InsertAsync(before, "dealer_settings", new()
            {
                ["id"] = Guid.NewGuid(),
                ["dealer_id"] = withActiveRow,
                ["dealer_name"] = "Paying",
                ["contact_email"] = "paying@x.com",
                ["is_active"] = true,
                ["slug"] = "paying-dealer",
                ["created_at"] = DateTime.UtcNow,
            });
            await InsertAsync(before, "dealer_subscriptions", new()
            {
                ["id"] = Guid.NewGuid(),
                ["dealer_id"] = withActiveRow,
                ["status"] = "Active",
                ["stripe_customer_id"] = "cus_paying",
                ["stripe_subscription_id"] = "sub_paying",
                ["plan_id"] = "price_1",
                ["current_period_start"] = DateTime.UtcNow.AddDays(-1),
                ["current_period_end"] = DateTime.UtcNow.AddDays(29),
            });
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
