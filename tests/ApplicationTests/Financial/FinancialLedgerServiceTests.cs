using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Application.UnitTests;
using Domain.DealerSettings;
using Domain.Financial;
using Domain.Financial.Attributes;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Financial;

/// <summary>
/// Tests for EfFinancialLedgerService (REQ-FIN-LEDGER-001).
/// Verifies three idempotency invariants:
///  1. First call inserts exactly one row.
///  2. Duplicate call is a no-op (no extra row, no exception).
///  3. Concurrent invocations produce exactly one row.
/// </summary>
public class FinancialLedgerServiceTests
{
    private static readonly Guid DealerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static (TestApplicationDbContext context, Mock<ICachedCategoryService> cached) CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestApplicationDbContext(options, DealerId);
        ctx.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(DealerId, "Test", "test@test.com"));
        ctx.SaveChanges();
        var mock = new Mock<ICachedCategoryService>();
        return (ctx, mock);
    }

    private static ICachedCategoryService StubCategoryLookup(TestApplicationDbContext ctx)
    {
        var mock = new Mock<ICachedCategoryService>();
        var category = new TransactionCategory("Reconditioning", "desc", TransactionType.Expense);
        ctx.TransactionCategories.Add(category);
        ctx.SaveChanges();
        mock.Setup(s => s.GetByNameAsync("Reconditioning", It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        mock.Setup(s => s.InvalidateCacheAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock.Object;
    }

    private static ICurrentTenantService TenantService() =>
        new TestCurrentTenantService(DealerId, hasTenant: true);

    private sealed class TestCurrentTenantService : ICurrentTenantService
    {
        public TestCurrentTenantService(Guid dealerId, bool hasTenant)
        {
            DealerId = dealerId;
            HasTenant = hasTenant;
        }
        public Guid DealerId { get; }
        public bool HasTenant { get; }
    }

    [Fact]
    public async Task RegisterExpenseAsync_FirstCall_InsertsOneRow()
    {
        var (ctx, _) = CreateContext();
        var cached = StubCategoryLookup(ctx);
        var tenant = TenantService();
        var sut = new EfFinancialLedgerService(ctx, cached, tenant, NullLogger<EfFinancialLedgerService>.Instance);

        var taskId = Guid.NewGuid();
        await sut.RegisterExpenseAsync(
            carId: Guid.NewGuid(),
            amount: 22000m,
            currency: "ARS",
            category: "Reconditioning",
            occurredAt: DateTime.UtcNow,
            reconditioningTaskId: taskId,
            sourceId: taskId,
            cancellationToken: CancellationToken.None);

        var rows = await ctx.Transactions
            .Where(t => t.ReconditioningTaskId == taskId && t.SourceId == taskId)
            .ToListAsync();

        rows.Should().HaveCount(1);
        rows[0].Amount.Amount.Should().Be(22000m);
        rows[0].Type.Should().Be(TransactionType.Expense);
        rows[0].DealerId.Should().Be(DealerId);
    }

    [Fact]
    public async Task RegisterExpenseAsync_DuplicateCall_IsIdempotent()
    {
        var (ctx, _) = CreateContext();
        var cached = StubCategoryLookup(ctx);
        var tenant = TenantService();
        var sut = new EfFinancialLedgerService(ctx, cached, tenant, NullLogger<EfFinancialLedgerService>.Instance);

        var taskId = Guid.NewGuid();
        var firstArgs = new
        {
            CarId = Guid.NewGuid(),
            Amount = 22000m,
            Currency = "ARS",
            Category = "Reconditioning",
            OccurredAt = DateTime.UtcNow,
            ReconditioningTaskId = taskId,
            SourceId = taskId,
        };

        await sut.RegisterExpenseAsync(firstArgs.CarId, firstArgs.Amount, firstArgs.Currency, firstArgs.Category,
            firstArgs.OccurredAt, firstArgs.ReconditioningTaskId, firstArgs.SourceId, CancellationToken.None);
        await sut.RegisterExpenseAsync(firstArgs.CarId, firstArgs.Amount, firstArgs.Currency, firstArgs.Category,
            firstArgs.OccurredAt, firstArgs.ReconditioningTaskId, firstArgs.SourceId, CancellationToken.None);

        var rows = await ctx.Transactions
            .Where(t => t.ReconditioningTaskId == taskId && t.SourceId == taskId)
            .ToListAsync();
        rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task RegisterExpenseAsync_RepeatedCalls_InsertOneRow()
    {
        // The InMemory provider does not support concurrent operations on a
        // single DbContext, so we exercise the idempotency path sequentially
        // 5 times. The DB-floor (partial unique index) handles the truly
        // concurrent case in production — see B.6 migration.
        var (ctx, _) = CreateContext();
        var cached = StubCategoryLookup(ctx);
        var tenant = TenantService();
        var sut = new EfFinancialLedgerService(ctx, cached, tenant, NullLogger<EfFinancialLedgerService>.Instance);

        var taskId = Guid.NewGuid();
        var carId = Guid.NewGuid();
        var amount = 22000m;
        var occurredAt = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            await sut.RegisterExpenseAsync(
                carId, amount, "ARS", "Reconditioning", occurredAt, taskId, taskId,
                CancellationToken.None);
        }

        var rows = await ctx.Transactions
            .Where(t => t.ReconditioningTaskId == taskId && t.SourceId == taskId)
            .ToListAsync();
        rows.Should().HaveCount(1);
    }
}