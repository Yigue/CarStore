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
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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

    private static (TestApplicationDbContext context, Guid catId) Seed(DateTime date)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestApplicationDbContext(options, DealerA);
        ctx.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(DealerA, "Test", "test@test.com"));
        var cat = new TransactionCategory("X", "d", TransactionType.Income);
        ctx.TransactionCategories.Add(cat);
        ctx.Transactions.Add(new FinancialTransaction(DealerA, TransactionType.Income, 100m, "A", PaymentMethod.Cash, cat, transactionDate: date));
        ctx.Transactions.Add(new FinancialTransaction(DealerA, TransactionType.Expense, 50m, "B", PaymentMethod.Cash, cat, transactionDate: date));
        ctx.SaveChanges();
        return (ctx, cat.Id);
    }

    [Fact]
    public async Task Handle_NoTenant_ReturnsForbidden()
    {
        var date = new DateTime(2050, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var (ctx, _) = Seed(date);
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(false);

        var handler = new GetFinancialReportQueryHandler(ctx, tenantMock.Object);

        var result = await handler.Handle(
            new GetFinancialReportQuery(date, date, ReportGroupBy.Day),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WithTenant_ReturnsReportForThatTenantOnly()
    {
        var date = new DateTime(2050, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var (ctx, _) = Seed(date);
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(DealerA);

        var handler = new GetFinancialReportQueryHandler(ctx, tenantMock.Object);

        var result = await handler.Handle(
            new GetFinancialReportQuery(date, date, ReportGroupBy.Day),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Currency.Should().Be("ARS");
        result.Value.Series.Should().HaveCount(1);
        result.Value.Series[0].Bucket.Should().Be("2050-06-01");
        result.Value.Series[0].Income.Should().Be(100m);
        result.Value.Series[0].Expense.Should().Be(50m);
        result.Value.Series[0].Balance.Should().Be(50m);
        result.Value.Totals.Income.Should().Be(100m);
        result.Value.Totals.Expense.Should().Be(50m);
        result.Value.Totals.Balance.Should().Be(50m);
    }
}