using System;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
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
/// sfc B0.1.5 (rollback): <c>AddMoneyCurrencyColumns.Down()</c> removes only its own columns and leaves the
/// rest of the chain intact, and the migration can be applied again afterwards. Runs on a throwaway database
/// against real Postgres so it is part of the Postgres category in CI (the manual check in B0.1.5 was a
/// one-off on a developer machine).
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class AddMoneyCurrencyColumnsMigrationPostgresTests(PostgresFixture fixture)
{
    private const string Target = "20261010152035_AddMoneyCurrencyColumns";
    private const string Previous = "20261008004837_TransactionCategoryTenancy";

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

    private static async Task<bool> ColumnExistsAsync(string connectionString, string table, string column)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name=@t AND column_name=@c";
        command.Parameters.AddWithValue("t", table);
        command.Parameters.AddWithValue("c", column);
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    [Fact]
    public async Task Down_RemovesTheCurrencyColumnsAndTheMigrationCanBeAppliedAgain()
    {
        string database = $"money_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(fixture.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{database}\"";
            await create.ExecuteNonQueryAsync();
        }

        string connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString()) { Database = database }.ConnectionString;

        await using var context = NewContext(connectionString);
        var migrator = context.GetService<IMigrator>();

        // Up to (and including) the migration under test.
        await migrator.MigrateAsync(Target);
        (await ColumnExistsAsync(connectionString, "cars", "price_currency")).Should().BeTrue();
        (await ColumnExistsAsync(connectionString, "sales", "final_price_currency")).Should().BeTrue();

        // Down() — back to the previous migration.
        await migrator.MigrateAsync(Previous);
        (await ColumnExistsAsync(connectionString, "cars", "price_currency")).Should().BeFalse();
        (await ColumnExistsAsync(connectionString, "sales", "final_price_currency")).Should().BeFalse();
        // purchase_cost_currency belongs to an earlier migration, so Down() must leave it alone.
        (await ColumnExistsAsync(connectionString, "cars", "purchase_cost_currency")).Should().BeTrue();
        // The rest of the table is untouched.
        (await ColumnExistsAsync(connectionString, "cars", "patente")).Should().BeTrue();

        // And it applies again, all the way to the latest.
        await context.Database.MigrateAsync();
        (await ColumnExistsAsync(connectionString, "cars", "price_currency")).Should().BeTrue();
    }
}
