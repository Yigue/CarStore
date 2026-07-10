using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Financial.GetSummary;
using Application.Abstractions.Tenancy;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Moq;
using Xunit;
using SharedKernel;

namespace Application.UnitTests.Financial;

public class GetFinancialSummaryQueryHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options, TestDealerId);
    }

    private static Mock<ICurrentTenantService> CreateTenantServiceMock(bool hasTenant = true, Guid? dealerId = null)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.SetupGet(t => t.HasTenant).Returns(hasTenant);
        mock.SetupGet(t => t.DealerId).Returns(dealerId ?? TestDealerId);
        return mock;
    }

    private static async Task SeedTestDataAsync(TestApplicationDbContext context, Guid dealerId)
    {
        var now = DateTime.UtcNow;

        // Create categories first
        var incomeCategory = new TransactionCategory("Sales Income", "Income from sales", TransactionType.Income);
        var expenseCategory = new TransactionCategory("Rent", "Office rent expense", TransactionType.Expense);
        context.TransactionCategories.AddRange(incomeCategory, expenseCategory);
        await context.SaveChangesAsync();

        // Add income transactions
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Income, 5000m, "Sale 1", PaymentMethod.BankTransfer, incomeCategory, null, null, null, now));
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Income, 3000m, "Sale 2", PaymentMethod.Cash, incomeCategory, null, null, null, now.AddDays(-2)));
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Income, 7000m, "Sale 3", PaymentMethod.CreditCard, incomeCategory, null, null, null, now.AddDays(-5)));

        // Add expense transactions
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Expense, 1000m, "Rent", PaymentMethod.BankTransfer, expenseCategory, null, null, null, now.AddDays(-1)));
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Expense, 500m, "Utilities", PaymentMethod.Other, expenseCategory, null, null, null, now.AddDays(-6)));

        // Add transaction for different dealer to test tenant isolation
        var otherDealerId = Guid.NewGuid();
        context.Transactions.Add(new FinancialTransaction(otherDealerId, TransactionType.Income, 9999m, "Other Dealer Income", PaymentMethod.Cash, incomeCategory, null, null, null, now));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ComputesTotalIncome_ForCurrentDealerOnly()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context, TestDealerId);
        var tenantMock = CreateTenantServiceMock();
        var handler = new GetFinancialSummaryQueryHandler(context, tenantMock.Object);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalIncome.Should().Be(15000m); // 5000 + 3000 + 7000 (excludes 9999 from other dealer)
    }

    [Fact]
    public async Task Handle_ComputesTotalExpenses_ForCurrentDealerOnly()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context, TestDealerId);
        var tenantMock = CreateTenantServiceMock();
        var handler = new GetFinancialSummaryQueryHandler(context, tenantMock.Object);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalExpenses.Should().Be(1500m); // 1000 + 500
    }

    [Fact]
    public async Task Handle_ComputesBalance()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context, TestDealerId);
        var tenantMock = CreateTenantServiceMock();
        var handler = new GetFinancialSummaryQueryHandler(context, tenantMock.Object);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Balance.Should().Be(13500m); // 15000 - 1500
    }

    [Fact]
    public async Task Handle_ComputesEntryCount()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context, TestDealerId);
        var tenantMock = CreateTenantServiceMock();
        var handler = new GetFinancialSummaryQueryHandler(context, tenantMock.Object);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EntryCount.Should().Be(5); // 3 income + 2 expense (excludes other dealer)
    }

    [Fact]
    public async Task Handle_ReturnsZeros_WhenNoTransactions()
    {
        using var context = CreateContext();
        var tenantMock = CreateTenantServiceMock();
        var handler = new GetFinancialSummaryQueryHandler(context, tenantMock.Object);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalIncome.Should().Be(0m);
        result.Value.TotalExpenses.Should().Be(0m);
        result.Value.Balance.Should().Be(0m);
        result.Value.EntryCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TenantGuard_Missing_ReturnsForbidden()
    {
        using var context = CreateContext();
        var tenantMock = CreateTenantServiceMock(hasTenant: false);
        var handler = new GetFinancialSummaryQueryHandler(context, tenantMock.Object);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WithFromToDateRange_NarrowsResults()
    {
        // Arrange
        using var context = CreateContext();
        await SeedTestDataAsync(context, TestDealerId);
        var tenantMock = CreateTenantServiceMock();
        var handler = new GetFinancialSummaryQueryHandler(context, tenantMock.Object);
        
        // Filter: only last 3 days (now, now - 1, now - 2)
        // This includes now, now - 1, now - 2, and excludes now - 5, now - 6
        var now = DateTime.UtcNow;
        var query = new GetFinancialSummaryQuery(now.AddDays(-3), now.AddMinutes(5));

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalIncome.Should().Be(8000m); // 5000 (now) + 3000 (now-2)
        result.Value.TotalExpenses.Should().Be(1000m); // 1000 (now-1)
        result.Value.EntryCount.Should().Be(3);
    }
}