using Application.Abstractions.Tenancy;
using Domain.Integrations;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Integrations;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace InfrastructureEfTests.Integrations;

/// <summary>sfc C1.1 / ADR C22: the persisted column is unreadable without the Data Protection keys.</summary>
public class DataProtectionCredentialPersistenceTests
{
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTime Now = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
    private const string Secret = "APP_USR-super-secret-refresh-token";

    private sealed class FakeTenant : ICurrentTenantService
    {
        public Guid DealerId => TenantId;
        public bool HasTenant => false;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private static ApplicationDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new ApplicationDbContext(options, new NoOpPublisher(), new FakeTenant());
    }

    [Fact]
    public async Task EncryptedCredentials_AreNotReadableDirectlyInTheDatabase_AndRoundTripThroughTheProtector()
    {
        var protector = new DataProtectionCredentialProtector(new EphemeralDataProtectionProvider());
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using (var ctx = NewContext(connection))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.DealerSettings.Add(new DealerSettingsEntity(TenantId, "Test Dealer", "test@dealer.com"));
            ctx.DealerIntegrations.Add(DealerIntegration.Connect(
                TenantId, "MercadoLibre", protector.Protect(Secret), "read write", Now.AddHours(6), Now));
            await ctx.SaveChangesAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT encrypted_credentials FROM dealer_integrations";
        string raw = (string)(await command.ExecuteScalarAsync())!;

        raw.Should().NotContain(Secret);
        raw.Should().NotBeNullOrWhiteSpace();

        await using var readCtx = NewContext(connection);
        DealerIntegration stored = await readCtx.DealerIntegrations.SingleAsync();
        stored.Provider.Should().Be("mercadolibre");
        protector.Unprotect(stored.EncryptedCredentials!).Should().Be(Secret);
    }
}
