using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Clients.Restore;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Clients;

public class RestoreClientCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_RestoreClient_WhenDeleted()
    {
        // Arrange
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 27, 20, 0, 0, DateTimeKind.Utc) };
        var actorId = Guid.NewGuid();
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(x => x.UserId).Returns(actorId);

        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, "John", "Doe", "123", "john@test.com", "555", "Street 1", dateProvider.UtcNow);
        client.Delete(actorId, dateProvider.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);
        mockTenantService.Setup(x => x.HasTenant).Returns(true);

        var handler = new RestoreClientCommandHandler(context, mockUserContext.Object, dateProvider, mockTenantService.Object);
        var command = new RestoreClientCommand(client.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(client.Id);

        var updated = await context.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == client.Id);
        updated.Should().NotBeNull();
        updated!.IsDeleted.Should().BeFalse();
        updated.DeletedAtUtc.Should().BeNull();
        updated.DeletedBy.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenClientBelongsToAnotherTenant()
    {
        // Arrange
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();
        var actorId = Guid.NewGuid();
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(x => x.UserId).Returns(actorId);

        var dealerId = Guid.NewGuid();
        var otherDealerId = Guid.NewGuid();
        var client = new Client(otherDealerId, "John", "Doe", "123", "john@test.com", "555", "Street 1", dateProvider.UtcNow);
        client.Delete(actorId, dateProvider.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);
        mockTenantService.Setup(x => x.HasTenant).Returns(true);

        var handler = new RestoreClientCommandHandler(context, mockUserContext.Object, dateProvider, mockTenantService.Object);
        var command = new RestoreClientCommand(client.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClientErrors.NotFound(client.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenClientNotDeleted()
    {
        // Arrange
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();
        var mockUserContext = new Mock<IUserContext>();
        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, "John", "Doe", "123", "john@test.com", "555", "Street 1", dateProvider.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);
        mockTenantService.Setup(x => x.HasTenant).Returns(true);

        var handler = new RestoreClientCommandHandler(context, mockUserContext.Object, dateProvider, mockTenantService.Object);
        var command = new RestoreClientCommand(client.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClientErrors.NotDeleted(client.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenClientNotFound()
    {
        // Arrange
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();
        var mockUserContext = new Mock<IUserContext>();
        var mockTenantService = new Mock<ICurrentTenantService>();
        var handler = new RestoreClientCommandHandler(context, mockUserContext.Object, dateProvider, mockTenantService.Object);
        var id = Guid.NewGuid();
        var command = new RestoreClientCommand(id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClientErrors.NotFound(id));
    }
}
