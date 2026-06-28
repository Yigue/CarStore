using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Clients.GetDeleted;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Clients;

public class GetDeletedClientsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext(Guid dealerId)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options, dealerId);
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyDeletedClients_ForCurrentTenant()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var otherDealerId = Guid.NewGuid();
        using var context = CreateContext(dealerId);

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);
        mockTenantService.Setup(x => x.HasTenant).Returns(true);

        var date = DateTime.UtcNow;

        // Client 1: Deleted, current tenant (should return)
        var client1 = new Client(dealerId, "John", "Doe", "123", "john@test.com", "555", "Street 1", date);
        client1.Delete(Guid.NewGuid(), date);
        context.Clients.Add(client1);

        // Client 2: Active, current tenant (should NOT return)
        var client2 = new Client(dealerId, "Jane", "Doe", "456", "jane@test.com", "555", "Street 2", date);
        context.Clients.Add(client2);

        // Client 3: Deleted, other tenant (should NOT return)
        var client3 = new Client(otherDealerId, "Bob", "Smith", "789", "bob@test.com", "555", "Street 3", date);
        client3.Delete(Guid.NewGuid(), date);
        context.Clients.Add(client3);

        await context.SaveChangesAsync();

        var handler = new GetDeletedClientsQueryHandler(context, mockTenantService.Object);
        var query = new GetDeletedClientsQuery(Page: 1, PageSize: 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be(client1.Id);
    }
}
