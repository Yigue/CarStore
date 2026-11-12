using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Caching;
using Application.Financial.Categories.Delete;
using Domain.Financial.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Financial;

public class DeleteCategoryCommandHandlerTests
{
    private static readonly Guid TestDealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static void SetDealerId(TransactionCategory category, Guid dealerId)
    {
        typeof(Entity).GetProperty(nameof(Entity.DealerId))!
            .SetValue(category, dealerId);
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_WhenCategoryIsSystemCategory()
    {
        using var context = CreateContext();
        var cacheMock = new Mock<ICachedCategoryService>();

        var category = new TransactionCategory("Sistema", "Categoría del sistema", TransactionType.Income);
        context.TransactionCategories.Add(category);
        await context.SaveChangesAsync();

        var handler = new DeleteCategoryCommandHandler(context, cacheMock.Object);
        var command = new DeleteCategoryCommand(category.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Category.SystemReadOnly");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenCategoryBelongsToDealerAndNotInUse()
    {
        using var context = CreateContext();
        var cacheMock = new Mock<ICachedCategoryService>();

        var category = new TransactionCategory("Propia", "Categoría propia", TransactionType.Income);
        SetDealerId(category, TestDealerId);
        context.TransactionCategories.Add(category);
        await context.SaveChangesAsync();

        var handler = new DeleteCategoryCommandHandler(context, cacheMock.Object);
        var command = new DeleteCategoryCommand(category.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var deleted = await context.TransactionCategories.FindAsync(category.Id);
        deleted.Should().BeNull();
        cacheMock.Verify(c => c.InvalidateCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCategoryDoesNotExist()
    {
        using var context = CreateContext();
        var cacheMock = new Mock<ICachedCategoryService>();

        var handler = new DeleteCategoryCommandHandler(context, cacheMock.Object);
        var command = new DeleteCategoryCommand(Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
