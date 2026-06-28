using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Clients.SoftDelete;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Clients;

public class SoftDeleteClientCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_SoftDeleteClient_WhenNotAlreadyDeleted()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 27, 20, 0, 0, DateTimeKind.Utc) };
        var actorId = Guid.NewGuid();
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(x => x.UserId).Returns(actorId);

        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, "Jane", "Doe", "999", "jane@test.com", "555", "Street 1", dateProvider.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        client.ClearDomainEvents();

        var handler = new SoftDeleteClientCommandHandler(context, mockUserContext.Object, dateProvider);
        var result = await handler.Handle(new SoftDeleteClientCommand(client.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(client.Id);

        var persisted = await context.Clients.IgnoreQueryFilters().FirstAsync(c => c.Id == client.Id);
        persisted.IsDeleted.Should().BeTrue();
        persisted.DeletedAtUtc.Should().Be(dateProvider.UtcNow);
        persisted.DeletedBy.Should().Be(actorId);
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_WhenAlreadyDeleted_Idempotent()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 27, 21, 0, 0, DateTimeKind.Utc) };
        var actorId = Guid.NewGuid();
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(x => x.UserId).Returns(actorId);

        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, "Mark", "Smith", "111", "mark@test.com", "555", "Street 1", dateProvider.UtcNow);
        client.Delete(actorId, dateProvider.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var handler = new SoftDeleteClientCommandHandler(context, mockUserContext.Object, dateProvider);
        var result = await handler.Handle(new SoftDeleteClientCommand(client.Id), CancellationToken.None);

        // Idempotent: already deleted, should still succeed
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(client.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenClientDoesNotExist()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();
        var mockUserContext = new Mock<IUserContext>();
        var handler = new SoftDeleteClientCommandHandler(context, mockUserContext.Object, dateProvider);
        var missingId = Guid.NewGuid();

        var result = await handler.Handle(new SoftDeleteClientCommand(missingId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClientErrors.NotFound(missingId));
    }
}
