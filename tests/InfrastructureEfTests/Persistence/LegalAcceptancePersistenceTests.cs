using Application.Abstractions.Tenancy;
using Domain.Legal;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace InfrastructureEfTests.Persistence;

/// <summary>A0.1 / RN-016: acceptance records are persisted and strictly append-only.</summary>
public class LegalAcceptancePersistenceTests
{
    private static readonly Guid TenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly string Hash = new('c', 64);

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

    private static async Task<SqliteConnection> OpenSchemaAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var ctx = NewContext(connection);
        await ctx.Database.EnsureCreatedAsync();
        ctx.DealerSettings.Add(new DealerSettingsEntity(TenantId, "Test Dealer", "test@dealer.com"));
        await ctx.SaveChangesAsync();
        return connection;
    }

    private static LegalAcceptance NewAcceptance() => LegalAcceptance.Create(
        TenantId, Guid.NewGuid(), LegalDocumentKind.Terms, "2026-10-01", Hash,
        new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc), "203.0.113.7", "UA/1");

    [Fact]
    public async Task Insert_RoundTripsAllFields()
    {
        await using var connection = await OpenSchemaAsync();
        var acceptance = NewAcceptance();
        await using (var ctx = NewContext(connection))
        {
            ctx.LegalAcceptances.Add(acceptance);
            await ctx.SaveChangesAsync();
        }

        await using var read = NewContext(connection);
        var loaded = await read.LegalAcceptances.AsNoTracking().SingleAsync();
        loaded.Id.Should().Be(acceptance.Id);
        loaded.DealerId.Should().Be(TenantId);
        loaded.DocumentKind.Should().Be(LegalDocumentKind.Terms);
        loaded.Version.Should().Be("2026-10-01");
        loaded.ContentSha256.Should().Be(Hash);
        loaded.AcceptedAtUtc.Should().Be(new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc));
        loaded.Ip.Should().Be("203.0.113.7");
    }

    [Fact]
    public async Task Update_IsRejected()
    {
        await using var connection = await OpenSchemaAsync();
        await using var ctx = NewContext(connection);
        var acceptance = NewAcceptance();
        ctx.LegalAcceptances.Add(acceptance);
        await ctx.SaveChangesAsync();

        ctx.Entry(acceptance).Property(a => a.Version).CurrentValue = "2030-01-01";
        var act = () => ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    public async Task Delete_IsRejected()
    {
        await using var connection = await OpenSchemaAsync();
        await using var ctx = NewContext(connection);
        var acceptance = NewAcceptance();
        ctx.LegalAcceptances.Add(acceptance);
        await ctx.SaveChangesAsync();

        ctx.LegalAcceptances.Remove(acceptance);
        var act = () => ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }
}
