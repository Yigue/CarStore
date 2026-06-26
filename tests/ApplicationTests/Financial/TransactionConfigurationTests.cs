using Application.UnitTests;
using Domain.DealerSettings;
using Domain.Financial;
using Domain.Financial.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Financial;

/// <summary>
/// Tests for the EF Core configuration of <see cref="FinancialTransaction"/>.
/// Asserts the two composite indexes for tenant-scoped hot paths and the
/// partial unique index for ledger idempotency are declared on the EF model.
/// </summary>
public class TransactionConfigurationTests
{
    private static readonly Guid DealerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new TestApplicationDbContext(options, DealerId);
        ctx.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(DealerId, "Test", "test@test.com"));
        return ctx;
    }

    [Fact]
    public void CompositeIndex_DealerId_TransactionDate_IsDeclaredOnEntity()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(FinancialTransaction));
        entityType.Should().NotBeNull();

        var indexes = entityType!.GetIndexes().Select(i => i.GetDatabaseName()).ToList();
        indexes.Should().Contain("IX_transactions_DealerId_TransactionDate");
    }

    [Fact]
    public void CompositeIndex_DealerId_CategoryId_IsDeclaredOnEntity()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(FinancialTransaction));
        entityType.Should().NotBeNull();

        var indexes = entityType!.GetIndexes().Select(i => i.GetDatabaseName()).ToList();
        indexes.Should().Contain("IX_transactions_DealerId_CategoryId");
    }

    [Fact]
    public void CompositeIndex_DealerId_TransactionDate_HasCorrectColumnOrder()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(FinancialTransaction));
        var idx = entityType!.GetIndexes()
            .Single(i => i.GetDatabaseName() == "IX_transactions_DealerId_TransactionDate");
        var propNames = idx.Properties.Select(p => p.Name).ToList();
        propNames.Should().Equal("DealerId", "TransactionDate");
    }

    [Fact]
    public void CompositeIndex_DealerId_CategoryId_HasCorrectColumnOrder()
    {
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(FinancialTransaction));
        var idx = entityType!.GetIndexes()
            .Single(i => i.GetDatabaseName() == "IX_transactions_DealerId_CategoryId");
        var propNames = idx.Properties.Select(p => p.Name).ToList();
        propNames.Should().Equal("DealerId", "CategoryId");
    }

    [Fact]
    public void PartialUniqueIndex_ReconditioningTaskId_SourceId_IsDeclaredOnEntity()
    {
        // REQ-FIN-LEDGER-001 (C.2): a unique partial index on (ReconditioningTaskId,
        // SourceId) is the DB floor for ledger idempotency. Asserted in C.2
        // (already passing here for B.6 wiring).
        using var ctx = CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(FinancialTransaction));
        var idx = entityType!.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "IX_transactions_ReconditioningTaskId_SourceId");
        idx.Should().NotBeNull();
        idx!.IsUnique.Should().BeTrue();
        var propNames = idx.Properties.Select(p => p.Name).ToList();
        propNames.Should().Equal("ReconditioningTaskId", "SourceId");
    }
}