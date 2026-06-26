using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Database;
using Domain.Financial;
using Domain.Financial.Attributes;
using Xunit;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Reports;

public class FinancialReportJsonShapeTests
{
    private sealed record TotalsDto(decimal Income, decimal Expense, decimal Balance);
    private sealed record SeriesDto(string Bucket, decimal Income, decimal Expense, decimal Balance);
    private sealed record ReportResponse(
        string Currency,
        string From,
        string To,
        string GroupBy,
        SeriesDto[] Series,
        TotalsDto Totals);

    [Fact]
    public async Task GetFinancialsReport_Daily_ReturnsCorrectShapeAndTotals()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Seed some data in a specific future range to avoid collisions
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var category = await context.TransactionCategories.FirstAsync();
        
        var t1 = new FinancialTransaction(
            IntegrationTestHelpers.DefaultDealerId,
            TransactionType.Income,
            1000m,
            "T1",
            PaymentMethod.Cash,
            category,
            transactionDate: new DateTime(2050, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        var t2 = new FinancialTransaction(
            IntegrationTestHelpers.DefaultDealerId,
            TransactionType.Expense,
            400m,
            "T2",
            PaymentMethod.Cash,
            category,
            transactionDate: new DateTime(2050, 6, 3, 12, 0, 0, DateTimeKind.Utc));

        context.Transactions.AddRange(t1, t2);
        await context.SaveChangesAsync();

        // Act
        var response = await client.GetAsync("/api/v1/reports/financials?from=2050-06-01&to=2050-06-07&groupBy=Day");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<ReportResponse>(IntegrationTestHelpers.JsonOptions);

        report.Should().NotBeNull();
        report!.Currency.Should().Be("ARS");
        report.GroupBy.Should().Be("Day");
        report.Series.Should().HaveCount(7);
        
        // Assert dates are ordered and formatted correctly
        report.Series[0].Bucket.Should().Be("2050-06-01");
        report.Series[0].Income.Should().Be(1000m);
        report.Series[0].Expense.Should().Be(0m);
        report.Series[0].Balance.Should().Be(1000m);

        report.Series[2].Bucket.Should().Be("2050-06-03");
        report.Series[2].Income.Should().Be(0m);
        report.Series[2].Expense.Should().Be(400m);
        report.Series[2].Balance.Should().Be(-400m);

        report.Totals.Income.Should().Be(1000m);
        report.Totals.Expense.Should().Be(400m);
        report.Totals.Balance.Should().Be(600m);
    }

    [Fact]
    public async Task GetFinancialsReport_Monthly_ReturnsCorrectMonths()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/reports/financials?from=2050-01-01&to=2050-03-31&groupBy=Month");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<ReportResponse>(IntegrationTestHelpers.JsonOptions);

        report.Should().NotBeNull();
        report!.Series.Should().HaveCount(3);
        report.Series[0].Bucket.Should().Be("2050-01");
        report.Series[1].Bucket.Should().Be("2050-02");
        report.Series[2].Bucket.Should().Be("2050-03");
    }

    [Fact]
    public async Task GetFinancialsReport_MissingDates_ReturnsBadRequest()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/reports/financials?groupBy=Month");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("MISSING_DATE_RANGE");
    }

    [Fact]
    public async Task GetFinancialsReport_InvalidGroupBy_ReturnsBadRequest()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/reports/financials?from=2050-01-01&to=2050-01-31&groupBy=Hour");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("INVALID_GROUP_BY");
    }
}
