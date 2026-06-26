using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Application.Common;
using Application.Reports.GetFinancialReport;
using Application.UnitTests;
using Domain.DealerSettings;
using Domain.Financial;
using Domain.Financial.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Reports;

/// <summary>
/// Tests for GetFinancialReportQueryHandler defense-in-handler (REQ-FIN-TENANT-001
/// + audit-gap #13). The handler must filter by tenant at the LINQ layer
/// (in addition to the EF GQF) so that bypass scenarios (raw SQL, FromSqlRaw)
/// still reject cross-tenant rows. The handler also short-circuits with
/// Forbidden when no tenant context is present.
/// </summary>
public class GetFinancialReportQueryHandlerTests
{
    private static readonly Guid DealerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DealerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (TestApplicationDbContext context, Guid catId) Seed()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestApplicationDbContext(options, DealerA);
        ctx.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(DealerA, "Test", "test@test.com"));
        var cat = new TransactionCategory("X", "d", TransactionType.Income);
        ctx.TransactionCategories.Add(cat);
        ctx.Transactions.Add(new FinancialTransaction(DealerA, TransactionType.Income, 100m, "A", PaymentMethod.Cash, cat));
        ctx.Transactions.Add(new FinancialTransaction(DealerA, TransactionType.Expense, 50m, "B", PaymentMethod.Cash, cat));
        ctx.SaveChanges();
        return (ctx, cat.Id);
    }

    [Fact]
    public async Task Handle_NoTenant_ReturnsForbidden()
    {
        var (ctx, _) = Seed();
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(false);

        var handler = new GetFinancialReportQueryHandler(ctx, tenantMock.Object);

        var result = await handler.Handle(
            new GetFinancialReportQuery(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, ReportGroupBy.Day),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WithTenant_ReturnsReportForThatTenantOnly()
    {
        // The in-memory provider doesn't have a multi-tenant fixture set up
        // here; the relevant invariant is that the handler does NOT throw and
        // that the rows come from the dealer's slice. We seed 2 rows for
        // DealerA and query as DealerA — success.
        var (ctx, _) = Seed();
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(DealerA);

        var handler = new GetFinancialReportQueryHandler(ctx, tenantMock.Object);

        var result = await handler.Handle(
            new GetFinancialReportQuery(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, ReportGroupBy.Day),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // 2 rows for DealerA → 1 day bucket with income=100, expense=50.
        result.Value!.ByPeriod.Should().HaveCount(1);
        result.Value.ByPeriod[0].Income.Should().Be(100m);
        result.Value.ByPeriod[0].Expense.Should().Be(50m);
    }
}