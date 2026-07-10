using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Application.Financial.Update;
using Application.UnitTests;
using Domain.DealerSettings;
using Domain.Financial;
using Domain.Financial.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Financial;

/// <summary>
/// Tests for UpdateFinancialCommandHandler defense-in-handler (REQ-FIN-TENANT-001).
/// Asserts a cross-tenant attacker cannot mutate another dealer's transaction
/// and that no SaveChangesAsync is invoked on rejection.
/// </summary>
public class UpdateFinancialCommandHandlerTests
{
    private static readonly Guid DealerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DealerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (TestApplicationDbContext context, Guid txId) SeedTransaction()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestApplicationDbContext(options, DealerA);
        ctx.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(DealerA, "Test", "test@test.com"));
        var category = new TransactionCategory("X", "desc", TransactionType.Income);
        ctx.TransactionCategories.Add(category);
        var tx = new FinancialTransaction(
            DealerA, TransactionType.Income, 1000m, "orig",
            PaymentMethod.Cash, category);
        ctx.Transactions.Add(tx);
        ctx.SaveChanges();
        return (ctx, tx.Id);
    }

    [Fact]
    public async Task Handle_CrossTenantUpdate_ReturnsForbidden_AndNeverSaves()
    {
        // Arrange: seed a transaction belonging to DealerA
        var (seededCtx, txId) = SeedTransaction();
        var categoryId = seededCtx.TransactionCategories.First().Id;

        // Tenant context: DealerB (attacker)
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(DealerB);

        // Spy SaveChangesAsync — pass the real DbContext for Transactions but
        // route save through Moq so we can verify it was never called.
        var saveCalls = 0;
        var spyCtx = new Mock<IApplicationDbContext>();
        spyCtx.Setup(c => c.Transactions).Returns(seededCtx.Transactions);
        spyCtx
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                saveCalls++;
                return Task.FromResult(0);
            });

        var handler = new UpdateFinancialCommandHandler(spyCtx.Object, tenantMock.Object);

        // Act
        var result = await handler.Handle(
            new UpdateFinancialCommand(
                txId,
                TransactionType.Expense,
                9999m,
                "malicious update",
                PaymentMethod.Cash,
                "REF",
                DateTime.UtcNow,
                categoryId,
                null, null, null),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);

        // Verify SaveChanges was NEVER called by the rejected handler
        saveCalls.Should().Be(0);

        // Verify the underlying transaction was NOT modified by re-querying
        var untouched = seededCtx.Transactions.AsNoTracking().First(t => t.Id == txId);
        untouched.Description.Should().Be("orig");
        untouched.Amount.Amount.Should().Be(1000m);
    }
}