using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.UnitTests;
using Domain.Financial;
using Domain.Financial.Attributes;
using FluentAssertions;
using Infrastructure.Database.SeedData;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Financial;

public class TransactionCategoriesSeederTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task SeedAsync_ShouldInsertExactly9Categories_OnFirstRun()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        await TransactionCategoriesSeeder.SeedAsync(context, CancellationToken.None);

        // Assert
        var categories = await context.TransactionCategories.IgnoreQueryFilters().ToListAsync();
        categories.Should().HaveCount(9);

        var names = categories.Select(c => c.Name).ToList();
        names.Should().Contain("Reconditioning");
        names.Should().Contain("VehicleSale");
    }

    [Fact]
    public async Task SeedAsync_ShouldBeIdempotent_OnMultipleRuns()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        await TransactionCategoriesSeeder.SeedAsync(context, CancellationToken.None);
        await TransactionCategoriesSeeder.SeedAsync(context, CancellationToken.None);

        // Assert
        var categories = await context.TransactionCategories.IgnoreQueryFilters().ToListAsync();
        categories.Should().HaveCount(9);
    }
}
