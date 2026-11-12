using Application.Abstractions.Tenancy;
using Application.Reports.GetFinancialReport;
using Domain.Currency;
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

    // --- sfc B0.3.1 / ADR C2: presentation currency converts with each operation's own frozen rate ---

    private static GetFinancialReportQuery PresentationQuery(string presentation) =>
        new(Day.AddDays(-10), Day.AddDays(10), ReportGroupBy.Month, PresentationCurrency: presentation);

    private static async Task<TestApplicationDbContext> SeedPresentationAsync()
    {
        var ctx = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            DealerId);
        var income = new TransactionCategory("Ventas", "d", TransactionType.Income);
        ctx.TransactionCategories.Add(income);
        await ctx.SaveChangesAsync();
        var frozen = new ExchangeRateSnapshot("USD", "ARS", 1000m, Day, Guid.NewGuid());
        ctx.Transactions.AddRange(
            new FinancialTransaction(DealerId, TransactionType.Income, Money.Of(1000m, CurrencyCode.Ars), "ars in", PaymentMethod.Cash, income, transactionDate: Day),
            new FinancialTransaction(DealerId, TransactionType.Income, Money.Of(50m, CurrencyCode.Usd), "usd sale", PaymentMethod.Cash, income, transactionDate: Day, exchangeRate: frozen),
            new FinancialTransaction(DealerId, TransactionType.Income, Money.Of(20m, CurrencyCode.Usd), "usd manual", PaymentMethod.Cash, income, transactionDate: Day));
        // The dealer's rate today differs from the frozen one: it must not matter.
        ctx.DealerExchangeRates.Add(DealerExchangeRate.Record(DealerId, CurrencyCode.Usd, CurrencyCode.Ars, 1500m, Day.AddDays(5), Guid.NewGuid()).Value);
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task Handle_Should_ConvertWithTheFrozenRate_NotTheCurrentOne()
    {
        await using var ctx = await SeedPresentationAsync();

        var result = await Handler(ctx).Handle(PresentationQuery("ars"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("ARS");
        // 1000 ARS as is + 50 USD * 1000 (frozen), not * 1500 (today's).
        result.Value.Totals.Should().Be(new FinancialReportTotalsDto(51_000m, 0m, 51_000m));
    }

    [Fact]
    public async Task Handle_Should_ReportWhatCouldNotBeConverted_InsteadOfUsingTodaysRate()
    {
        await using var ctx = await SeedPresentationAsync();

        var result = await Handler(ctx).Handle(PresentationQuery("ARS"), CancellationToken.None);

        result.Value.Unconverted.Should().NotBeNull();
        result.Value.Unconverted!.Count.Should().Be(1);
        result.Value.Unconverted.ByCurrency.Should().ContainSingle()
            .Which.Should().Be(new FinancialReportUnconvertedDto.Entry("USD", 20m, 0m));
    }

    [Fact]
    public async Task Handle_Should_CountNativeUsdAsIs_AndLeaveArsWithoutSnapshotUnconverted_InUsd()
    {
        await using var ctx = await SeedPresentationAsync();

        var inUsd = await Handler(ctx).Handle(PresentationQuery("USD"), CancellationToken.None);

        // 50 USD as is (+20 USD manual, native); the ARS row has no snapshot so it is unconverted.
        inUsd.Value.Totals.Should().Be(new FinancialReportTotalsDto(70m, 0m, 70m));
        inUsd.Value.Unconverted!.Count.Should().Be(1);
        inUsd.Value.Unconverted.ByCurrency.Single().Currency.Should().Be("ARS");
    }

    [Fact]
    public async Task Handle_Should_RejectAnUnsupportedPresentationCurrency()
    {
        await using var ctx = await SeedPresentationAsync();

        var result = await Handler(ctx).Handle(PresentationQuery("EUR"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Unsupported");
    }

    [Fact]
    public async Task Handle_Should_NotAddUnconverted_WithoutAPresentationCurrency()
    {
        await using var ctx = await SeedPresentationAsync();

        var result = await Handler(ctx).Handle(Query(currency: null), CancellationToken.None);

        result.Value.Unconverted.Should().BeNull();
        result.Value.Totals.Income.Should().Be(1000m);
    }
}
