using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Abstractions.Tenancy;
using Application.Platform.AuditLogs;
using Application.Users.OwnerTransfer;
using Application.Users.OwnerTransfer.AcceptOwnerTransfer;
using Application.Users.Verification;
using Domain.Platform;
using Domain.Users;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace InfrastructureEfTests.Users;

/// <summary>
/// sfc A4.3 follow-up: accepting an owner transfer is ONE transaction (re-check owner + token, consume, swap, audit, save).
/// Runs on SQLite (a real relational transaction). SQLite has no row lock (<c>SELECT ... FOR UPDATE</c> is PostgreSQL
/// only), so these tests prove the re-check and the rollback, not the lock itself.
/// </summary>
public class OwnerTransferAcceptAtomicityTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DealerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

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

    /// <summary>Throws on the save that writes the audit entry, i.e. AFTER the token was consumed, inside the transaction.</summary>
    private sealed class FailOnAuditInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<PlatformAuditLogEntry>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("forced failure before commit");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>Runs <paramref name="onFirstUsersRead"/> once, right before the first read of the users table.</summary>
    private sealed class BeforeFirstUsersReadInterceptor(Func<Task> onFirstUsersRead) : DbCommandInterceptor
    {
        private bool _fired;

        public override async ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!_fired && command.CommandText.Contains("FROM \"users\"", StringComparison.OrdinalIgnoreCase))
            {
                _fired = true;
                await onFirstUsersRead();
            }
            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static ApplicationDbContext NewContext(SqliteConnection connection, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptors)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new ApplicationDbContext(options, new NoOpPublisher(), new NoTenant());
    }

    private sealed record Seeded(Guid OwnerId, Guid TargetId, Guid OtherAdminId, string PlainToken);

    private static async Task<Seeded> SeedAsync(ApplicationDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        var dealer = new DealerSettingsEntity(DealerId, DealerId, "Autos Sur SA", "contacto@autossur.com");
        var admin = new Role(DealerId, "Admin", "Admin");
        db.DealerSettings.Add(dealer);
        db.Roles.Add(admin);
        var owner = new User(DealerId, "owner@autossur.com", "Ana", "Apellido", "hash", admin.Id);
        var target = new User(DealerId, "nuevo@autossur.com", "Beto", "Apellido", "hash", admin.Id);
        var other = new User(DealerId, "otro@autossur.com", "Otro", "Apellido", "hash", admin.Id);
        foreach (User u in new[] { owner, target, other })
        {
            u.MarkEmailVerified(Now.AddDays(-30));
            db.Users.Add(u);
        }
        dealer.SetOwner(owner.Id);
        await db.SaveChangesAsync();

        string plain = UserActionTokenHasher.NewToken();
        db.UserActionTokens.Add(UserActionToken.Issue(
            [], UserActionPurpose.OwnerTransfer, DealerId, target.Email.Value, UserActionTokenHasher.Hash(plain),
            target.Id, null, owner.Id, Now));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Seeded(owner.Id, target.Id, other.Id, plain);
    }

    private static AcceptOwnerTransferCommandHandler Handler(ApplicationDbContext db, Guid actorId)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        var user = new Mock<IUserContext>();
        user.SetupGet(u => u.UserId).Returns(actorId);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(Now);
        return new AcceptOwnerTransferCommandHandler(
            db, tenant.Object, user.Object, new Mock<ISubscriptionGateway>().Object, new PlatformAuditLogRecorder(db),
            clock.Object, NullLogger<AcceptOwnerTransferCommandHandler>.Instance);
    }

    private static async Task<Guid?> OwnerAsync(ApplicationDbContext db) =>
        (await db.DealerSettings.IgnoreQueryFilters().AsNoTracking().SingleAsync()).OwnerUserId;

    [Fact]
    public async Task Accept_WhenTheSaveFailsAfterTheConsume_RollsBack_TokenStaysPending_OwnerUnchanged()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var seedDb = NewContext(connection);
        Seeded s = await SeedAsync(seedDb);

        await using var db = NewContext(connection, new FailOnAuditInterceptor());
        Func<Task> act = () => Handler(db, s.TargetId).Handle(new AcceptOwnerTransferCommand(s.PlainToken), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("forced failure before commit");

        await using var verify = NewContext(connection);
        (await verify.UserActionTokens.SingleAsync()).ConsumedAtUtc.Should().BeNull("the consume must roll back with the failed save");
        (await OwnerAsync(verify)).Should().Be(s.OwnerId);
        (await verify.PlatformAuditLogs.IgnoreQueryFilters().CountAsync()).Should().Be(0);

        // The token is still usable: a later accept without the fault succeeds.
        await using var retry = NewContext(connection);
        Result again = await Handler(retry, s.TargetId).Handle(new AcceptOwnerTransferCommand(s.PlainToken), CancellationToken.None);
        again.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Accept_WhenTheOwnerChangesBetweenTheFirstReadAndTheTransactionalRecheck_IsRefused_AndDoesNotSwap()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var seedDb = NewContext(connection);
        Seeded s = await SeedAsync(seedDb);

        // A second context flips the owner after the handler's first dealer read, before its transaction.
        var flip = new BeforeFirstUsersReadInterceptor(async () =>
        {
            await using ApplicationDbContext other = NewContext(connection);
            await other.DealerSettings.IgnoreQueryFilters().ExecuteUpdateAsync(
                x => x.SetProperty(d => d.OwnerUserId, (Guid?)s.OtherAdminId));
        });
        await using var db = NewContext(connection, flip);

        Result result = await Handler(db, s.TargetId).Handle(new AcceptOwnerTransferCommand(s.PlainToken), CancellationToken.None);

        result.Error.Should().Be(OwnerTransferErrors.InvalidToken);
        await using var verify = NewContext(connection);
        (await OwnerAsync(verify)).Should().Be(s.OtherAdminId, "the concurrent owner wins; the stale accept must not overwrite it");
        (await verify.UserActionTokens.SingleAsync()).ConsumedAtUtc.Should().BeNull("a refused accept does not spend the token");
        (await verify.PlatformAuditLogs.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Accept_HappyPath_SwapsOwner_ConsumesOnce_AndWritesTheAudit_ThenReplayIsRefused()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var seedDb = NewContext(connection);
        Seeded s = await SeedAsync(seedDb);

        await using var db = NewContext(connection);
        Result first = await Handler(db, s.TargetId).Handle(new AcceptOwnerTransferCommand(s.PlainToken), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        await using var verify = NewContext(connection);
        (await OwnerAsync(verify)).Should().Be(s.TargetId);
        (await verify.UserActionTokens.SingleAsync()).ConsumedAtUtc.Should().Be(Now);
        PlatformAuditLogEntry audit = await verify.PlatformAuditLogs.IgnoreQueryFilters().SingleAsync();
        audit.Action.Should().Be(PlatformAuditAction.OwnerTransferred);

        await using var db2 = NewContext(connection);
        Result replay = await Handler(db2, s.TargetId).Handle(new AcceptOwnerTransferCommand(s.PlainToken), CancellationToken.None);

        replay.Error.Should().Be(OwnerTransferErrors.InvalidToken);
        (await verify.PlatformAuditLogs.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }
}
