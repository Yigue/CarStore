using Application.Abstractions.Tenancy;
using Application.Reports.GetFinancialReport;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Reports;

/// <summary>
/// sfc B0.3.1 / ADR C1: the financial report never adds amounts of different currencies together. It reports one
/// currency at a time (ARS unless another is asked for) and lists every currency present in the window.
/// </summary>
public class GetFinancialReportCurrencyTests
{
    private static readonly Guid DealerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTime Day = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    private static async Task<TestApplicationDbContext> SeedAsync()
    {
        var ctx = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            DealerId);
        var income = new TransactionCategory("Ventas", "d", TransactionType.Income);
        var expense = new TransactionCategory("Gastos", "d", TransactionType.Expense);
        ctx.TransactionCategories.AddRange(income, expense);
        ctx.Transactions.AddRange(
            new FinancialTransaction(DealerId, TransactionType.Income, Money.Of(1000m, CurrencyCode.Ars), "ars in", PaymentMethod.Cash, income, transactionDate: Day),
            new FinancialTransaction(DealerId, TransactionType.Expense, Money.Of(300m, CurrencyCode.Ars), "ars out", PaymentMethod.Cash, expense, transactionDate: Day),
            new FinancialTransaction(DealerId, TransactionType.Income, Money.Of(50m, CurrencyCode.Usd), "usd in", PaymentMethod.Cash, income, transactionDate: Day));
        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static GetFinancialReportQueryHandler Handler(TestApplicationDbContext ctx)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return new GetFinancialReportQueryHandler(ctx, tenant.Object);
    }

    private static GetFinancialReportQuery Query(string? currency) =>
        new(Day.AddDays(-10), Day.AddDays(10), ReportGroupBy.Month, currency);

    [Fact]
    public async Task Handle_Should_ReportOnlyArs_ByDefault_AndListEveryCurrency()
    {
        await using var ctx = await SeedAsync();

        var result = await Handler(ctx).Handle(Query(currency: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("ARS");
        result.Value.Totals.Should().Be(new FinancialReportTotalsDto(1000m, 300m, 700m));
        result.Value.AvailableCurrencies.Should().BeEquivalentTo("ARS", "USD");
    }

    [Fact]
    public async Task Handle_Should_ReportTheRequestedCurrencyOnly()
    {
        await using var ctx = await SeedAsync();

        var result = await Handler(ctx).Handle(Query("usd"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("USD");
        result.Value.Totals.Should().Be(new FinancialReportTotalsDto(50m, 0m, 50m));
    }

    [Fact]
    public async Task Handle_Should_RejectAnUnsupportedCurrency()
    {
        await using var ctx = await SeedAsync();

        var result = await Handler(ctx).Handle(Query("EUR"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Unsupported");
    }
}
