using Application.Abstractions.Tenancy;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace InfrastructureEfTests.Persistence;

/// <summary>sfc A4.1: the single-use guarantee lives in the database (UPDATE ... WHERE consumed_at IS NULL, rowcount = 1).</summary>
public class UserActionTokenPersistenceTests
{
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTime Now = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

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

    private static async Task<(SqliteConnection Connection, Guid TokenId)> SeedAsync(DateTime issuedAt)
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var ctx = NewContext(connection);
        await ctx.Database.EnsureCreatedAsync();
        ctx.DealerSettings.Add(new DealerSettingsEntity(TenantId, "Test Dealer", "test@dealer.com"));
        var token = UserActionToken.Issue([], UserActionPurpose.EmailVerification, TenantId, "ana@test.com",
            new string('A', 64), Guid.NewGuid(), null, null, issuedAt);
        ctx.UserActionTokens.Add(token);
        await ctx.SaveChangesAsync();
        return (connection, token.Id);
    }

    [Fact]
    public async Task TryConsume_UsingTwoContexts_OnlyTheFirstOneWins()
    {
        var (connection, tokenId) = await SeedAsync(Now);
        await using var _ = connection;
        await using var first = NewContext(connection);
        await using var second = NewContext(connection);

        bool a = await first.TryConsumeUserActionTokenAsync(tokenId, Now.AddMinutes(1));
        bool b = await second.TryConsumeUserActionTokenAsync(tokenId, Now.AddMinutes(1));

        a.Should().BeTrue();
        b.Should().BeFalse();
        await using var read = NewContext(connection);
        (await read.UserActionTokens.AsNoTracking().SingleAsync()).ConsumedAtUtc.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public async Task TryConsume_RejectsAnExpiredToken_AndLeavesItUnconsumed()
    {
        var (connection, tokenId) = await SeedAsync(Now);
        await using var _ = connection;
        await using var ctx = NewContext(connection);

        bool consumed = await ctx.TryConsumeUserActionTokenAsync(tokenId, Now.AddHours(25));

        consumed.Should().BeFalse();
        (await ctx.UserActionTokens.AsNoTracking().SingleAsync()).ConsumedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task TokenHash_IsUnique()
    {
        var (connection, _) = await SeedAsync(Now);
        await using var _c = connection;
        await using var ctx = NewContext(connection);
        ctx.UserActionTokens.Add(UserActionToken.Issue([], UserActionPurpose.Invitation, TenantId, "bea@test.com",
            new string('A', 64), null, null, null, Now));

        Func<Task> act = () => ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
