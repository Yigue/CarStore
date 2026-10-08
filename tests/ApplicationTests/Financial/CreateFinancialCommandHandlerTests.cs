using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Caching;
using Application.Abstractions.Tenancy;
using Application.Financial.Create;
using Domain.Financial;
using Domain.Financial.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Financial;

public class CreateFinancialCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_NoTenantContext_ReturnsForbidden()
    {
        // API-005: Missing tenant context must return Forbidden
        using var context = CreateContext();
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(false);

        var cachedCategoryService = new Mock<ICachedCategoryService>();

        var handler = new CreateFinancialCommandHandler(
            context,
            cachedCategoryService.Object,
            tenantMock.Object);

        var command = new CreateFinancialCommand(
            TransactionType.Income,
            100m,
            "Deposit",
            PaymentMethod.Cash,
            Guid.NewGuid(),
            null,
            null,
            null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }
}
