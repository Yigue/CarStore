using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Clients.Delete;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Clients;

public class DeleteClientCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_SoftDeleteClient_WhenFound()
    {
        // Arrange
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 27, 20, 0, 0, DateTimeKind.Utc) };
        var actorId = Guid.NewGuid();
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(x => x.UserId).Returns(actorId);

        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, "John", "Doe", "123", "john@test.com", "555", "Street 1", dateProvider.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var handler = new DeleteClientCommandHandler(context, mockUserContext.Object, dateProvider);
        var command = new DeleteClientCommand(client.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(client.Id);

        // Client must still exist in Db (not hard removed) but have IsDeleted = true
        var updated = await context.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == client.Id);
        updated.Should().NotBeNull();
        updated!.IsDeleted.Should().BeTrue();
        updated.DeletedAtUtc.Should().Be(dateProvider.UtcNow);
        updated.DeletedBy.Should().Be(actorId);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenClientNotFound()
    {
        // Arrange
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();
        var mockUserContext = new Mock<IUserContext>();
        var handler = new DeleteClientCommandHandler(context, mockUserContext.Object, dateProvider);
        var id = Guid.NewGuid();
        var command = new DeleteClientCommand(id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClientErrors.NotFound(id));
    }
}
