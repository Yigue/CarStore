using Application.Abstractions.Tenancy;
using Domain.Leads;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfrastructureEfTests.Leads;

/// <summary>sfc C0.1 / RN-045: the first touch round-trips on the leads table; a lead without one reads back null.</summary>
public class LeadAttributionPersistenceTests
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

    [Fact]
    public async Task Attribution_Should_RoundTrip_AndStayNullWhenAbsent()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var touch = LeadAttribution.Create("google", "cpc", "otono", null, null, "https://google.com/", "/catalogo", "G1", null,
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        Guid withId, withoutId;
        await using (var write = NewContext(connection))
        {
            await write.Database.EnsureCreatedAsync();
            write.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(TenantId, "Test Dealer", "test@dealer.com"));
            await write.SaveChangesAsync();
            var with = Lead.Create(TenantId, "Ana", "ana@x.com", "1", LeadSource.Google, DateTime.UtcNow);
            with.SetFirstTouch(touch);
            var without = Lead.Create(TenantId, "Bob", "bob@x.com", "2", LeadSource.Web, DateTime.UtcNow);
            write.Leads.AddRange(with, without);
            await write.SaveChangesAsync();
            (withId, withoutId) = (with.Id, without.Id);
        }

        await using var read = NewContext(connection);
        (await read.Leads.AsNoTracking().SingleAsync(l => l.Id == withId)).Attribution.Should().Be(touch);
        (await read.Leads.AsNoTracking().SingleAsync(l => l.Id == withoutId)).Attribution.Should().BeNull();
    }
}
