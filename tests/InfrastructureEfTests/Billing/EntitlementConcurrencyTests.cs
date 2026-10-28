using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Domain.Billing;
using Domain.Cars;
using Domain.Cars.Attributes;
using Infrastructure.Common;
using Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SharedKernel;
using Xunit;

namespace InfrastructureEfTests.Billing;

/// <summary>
/// sfc A3.1.2 (A1, A21, RN-011): two concurrent <c>EnsureCanAddAsync</c> calls at exactly one-below-limit
/// must serialize through <c>pg_advisory_xact_lock</c> so exactly one succeeds — this is Postgres-only raw
/// SQL (see <see cref="EntitlementServiceHandler"/>/<see cref="IEntitlementService"/>) and has no SQLite
/// or InMemory equivalent, unlike every other InfrastructureEfTests concurrency test in this project
/// (e.g. <c>DealerSequenceConcurrencyTests</c>), which run on relational SQLite.
///
/// INFRA GAP (honest note, sfc A3.1.2): this project (<c>InfrastructureEfTests.csproj</c>) has no
/// Postgres-backed test fixture wired in — <c>Testcontainers.PostgreSql</c>/<c>PostgresFixture</c> only
/// exist in <c>tests/WebApiTests/Postgres</c>, which this project does not reference, and adding that
/// package reference requires editing <c>InfrastructureEfTests.csproj</c>, which was outside the allowed
/// edit surface for this change. This test therefore resolves a real Postgres connection string from the
/// <c>ENTITLEMENT_CONCURRENCY_TEST_PG_CONNECTION</c> environment variable and fails loudly (not a silent
/// skip) when it is not set, instead of fabricating a passing run — see tasks.md for the exact status.
/// </summary>
public class EntitlementConcurrencyTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private sealed class FakeTenant : ICurrentTenantService
    {
        public Guid DealerId => EntitlementConcurrencyTests.DealerId;
        public bool HasTenant => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private static string? ResolveConnectionString() =>
        new ConfigurationBuilder().AddEnvironmentVariables().Build()["ENTITLEMENT_CONCURRENCY_TEST_PG_CONNECTION"];

    private static ApplicationDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .Options,
            new NoOpPublisher(), new FakeTenant());

    private static IEntitlementService NewEntitlementService(ApplicationDbContext context)
    {
        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanLimits(MaxVehicles: 1, MaxUsers: null, MaxApiKeys: null, MaxWebhooks: null));
        return new EntitlementServiceHandler(context, context, planCatalog.Object);
    }

    [Fact]
    public async Task ConcurrentEnsureCanAddAsync_AtExactlyOneBelowTheLimit_ExactlyOneSucceeds()
    {
        string? connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.True(
                false,
                "sfc A3.1.2: no real Postgres connection string configured. Set " +
                "ENTITLEMENT_CONCURRENCY_TEST_PG_CONNECTION to a reachable Postgres instance to run this " +
                "test — pg_advisory_xact_lock is Postgres-only and cannot be exercised on SQLite/InMemory. " +
                "See the type doc comment for why this project has no self-hosted Postgres fixture yet.");
            return;
        }

        await using var migrationCtx = NewContext(connectionString);
        await migrationCtx.Database.MigrateAsync();
        migrationCtx.DealerSubscriptions.Add(
            DealerSubscription.CreatePending(DealerId, null, planId: string.Empty, planCode: "standard"));
        // Marca.Nombre has a globally-unique index — a fixed name would collide across repeated runs
        // against the same persistent Postgres instance.
        var marca = new Marca($"Toyota-{Guid.NewGuid():N}");
        var modelo = new Modelo("Corolla", marca.Id);
        migrationCtx.Marca.Add(marca);
        migrationCtx.Modelo.Add(modelo);
        // No vehicle yet -> at MaxVehicles=1, the dealer is exactly one-below the limit: the FIRST
        // concurrent add must succeed and the SECOND must be rejected, never both.
        await migrationCtx.SaveChangesAsync();

        const int callers = 2;
        Task<Result>[] tasks = Enumerable.Range(0, callers)
            .Select(async i =>
            {
                await using ApplicationDbContext ctx = NewContext(connectionString);
                await using var transaction = await ctx.Database.BeginTransactionAsync();
                IEntitlementService service = NewEntitlementService(ctx);
                Result result = await service.EnsureCanAddAsync(DealerId, PlanResource.Vehicles, CancellationToken.None);
                if (result.IsSuccess)
                {
                    // Mirrors the real handler: the INSERT happens inside the same transaction, right
                    // after the check, before the lock-releasing commit.
                    // Marca/Modelo already exist (seeded once, above, on a different DbContext). Query
                    // them fresh on THIS context so they come back properly tracked as Unchanged — Marca
                    // has a globally-unique name index, so any re-INSERT attempt fails outright.
                    var marcaRef = await ctx.Marca.FirstAsync(m => m.Id == marca.Id, cancellationToken: default);
                    var modeloRef = await ctx.Modelo.FirstAsync(m => m.Id == modelo.Id, cancellationToken: default);
                    var car = new Car(
                        DealerId, marcaRef, modeloRef, Color.Blue, TypeCar.Sedan, StatusCar.New,
                        StatusServiceCar.Disponible, 4, 5, 2000, 0, 2024, $"AAA{i:000}", "concurrency test",
                        20000m, DateTime.UtcNow);
                    ctx.Cars.Add(car);
                    await ctx.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                else
                {
                    await transaction.RollbackAsync();
                }

                return result;
            })
            .ToArray();

        Result[] results = await Task.WhenAll(tasks);

        results.Count(r => r.IsSuccess).Should().Be(1, "the advisory lock must serialize both checks so only one add wins at the limit");
        results.Count(r => r.IsFailure).Should().Be(1);
    }
}
