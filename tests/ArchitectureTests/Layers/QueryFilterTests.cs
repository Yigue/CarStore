using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Domain.Clients;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Xunit;

namespace ArchitectureTests.Layers;

public class QueryFilterTests : BaseTest
{
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class NoOpTenantService : ICurrentTenantService
    {
        public Guid DealerId => Guid.Empty;
        public bool HasTenant => false;
    }

    [Fact]
    public void Every_SoftDeletable_Entity_Should_Have_SoftDelete_QueryFilter()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options, new NoOpPublisher(), new NoOpTenantService());

        // Act & Assert
        var entityTypes = context.Model.GetEntityTypes();
        var softDeletableEntitiesCount = 0;

        foreach (var entityType in entityTypes)
        {
            var clrType = entityType.ClrType;
            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                softDeletableEntitiesCount++;
                var queryFilter = entityType.GetQueryFilter();
                queryFilter.Should().NotBeNull($"Entity {clrType.Name} implements ISoftDeletable but has no query filter configured.");
                
                var filterString = queryFilter!.ToString();
                filterString.Should().Contain("IsDeleted");
            }
        }

        softDeletableEntitiesCount.Should().BeGreaterThan(0, "There should be at least one ISoftDeletable entity in the model (e.g., Client).");
    }
}
