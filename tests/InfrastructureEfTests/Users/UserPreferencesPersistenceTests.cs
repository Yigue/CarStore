using Application.Abstractions.Tenancy;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfrastructureEfTests.Users;

/// <summary>sfc B5.3 / ADR C21: the preferences document round-trips through the JSON column and dies with its user.</summary>
public class UserPreferencesPersistenceTests
{
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

    private static ApplicationDbContext NewContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                .Options,
            new NoOpPublisher(), new NoTenant());

    [Fact]
    public async Task Preferences_Should_RoundTripThroughTheJsonColumn()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using (var ctx = NewContext(connection))
            await ctx.Database.EnsureCreatedAsync();

        // The row's FK to users is not what this test is about: switch FK checks off instead of seeding a full user.
        var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF";
        await pragma.ExecuteNonQueryAsync();

        Guid userId = Guid.NewGuid();
        await using (var seed = NewContext(connection))
        {
            seed.UserPreferences.Add(UserPreferences.Create(userId, new PreferencesV1("year_desc", "list", 48, false, true, false)));
            await seed.SaveChangesAsync();
        }

        await using var read = NewContext(connection);
        var loaded = await read.UserPreferences.AsNoTracking().SingleAsync(p => p.UserId == userId);
        loaded.Preferences.Should().Be(new PreferencesV1("year_desc", "list", 48, false, true, false));
        loaded.SchemaVersion.Should().Be(UserPreferences.CurrentSchemaVersion);
    }
}
