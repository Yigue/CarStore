using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Application.Financial.Delete;
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
/// Tests for DeleteFinancialCommandHandler defense-in-handler (REQ-FIN-TENANT-001).
/// Mirrors UpdateFinancialCommandHandlerTests: cross-tenant delete is rejected
/// with Forbidden; Remove + SaveChangesAsync are NEVER called.
/// </summary>
public class DeleteFinancialCommandHandlerTests
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
    public async Task Handle_CrossTenantDelete_ReturnsForbidden_AndNeverSaves()
    {
        var (seededCtx, txId) = SeedTransaction();

        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(DealerB);

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

        var handler = new DeleteFinancialCommandHandler(spyCtx.Object, tenantMock.Object);

        var result = await handler.Handle(
            new DeleteFinancialCommand(txId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        saveCalls.Should().Be(0);

        // Underlying row still present
        var stillThere = seededCtx.Transactions.AsNoTracking().FirstOrDefault(t => t.Id == txId);
        stillThere.Should().NotBeNull();
    }
}