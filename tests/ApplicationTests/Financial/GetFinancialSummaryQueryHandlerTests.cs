using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Financial.GetSummary;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Financial;

public class GetFinancialSummaryQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task SeedTestDataAsync(TestApplicationDbContext context)
    {
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Create categories first
        var incomeCategory = new TransactionCategory("Sales Income", "Income from sales", TransactionType.Income);
        var expenseCategory = new TransactionCategory("Rent", "Office rent expense", TransactionType.Expense);
        context.TransactionCategories.AddRange(incomeCategory, expenseCategory);
        await context.SaveChangesAsync();

        // Add income transactions
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Income, 5000m, "Sale 1", PaymentMethod.BankTransfer, incomeCategory, null, null, null, now));
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Income, 3000m, "Sale 2", PaymentMethod.Cash, incomeCategory, null, null, null, now));
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Income, 7000m, "Sale 3", PaymentMethod.CreditCard, incomeCategory, null, null, null, now));

        // Add expense transactions
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Expense, 1000m, "Rent", PaymentMethod.BankTransfer, expenseCategory, null, null, null, now));
        context.Transactions.Add(new FinancialTransaction(dealerId, TransactionType.Expense, 500m, "Utilities", PaymentMethod.Other, expenseCategory, null, null, null, now));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ComputesTotalIncome()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetFinancialSummaryQueryHandler(context);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalIncome.Should().Be(15000m); // 5000 + 3000 + 7000
    }

    [Fact]
    public async Task Handle_ComputesTotalExpenses()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetFinancialSummaryQueryHandler(context);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalExpenses.Should().Be(1500m); // 1000 + 500
    }

    [Fact]
    public async Task Handle_ComputesBalance()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetFinancialSummaryQueryHandler(context);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Balance.Should().Be(13500m); // 15000 - 1500
    }

    [Fact]
    public async Task Handle_ComputesEntryCount()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetFinancialSummaryQueryHandler(context);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EntryCount.Should().Be(5); // 3 income + 2 expense
    }

    [Fact]
    public async Task Handle_ReturnsZeros_WhenNoTransactions()
    {
        using var context = CreateContext();
        var handler = new GetFinancialSummaryQueryHandler(context);
        var query = new GetFinancialSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalIncome.Should().Be(0m);
        result.Value.TotalExpenses.Should().Be(0m);
        result.Value.Balance.Should().Be(0m);
        result.Value.EntryCount.Should().Be(0);
    }
}