using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Clients.Update;
using Domain.Clients;
using Domain.Clients.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Clients;

public class UpdateClientCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_UpdateClient_WhenFound()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var client = new Client("John", "Doe", "123", "john@test.com", "555", "Street 1");
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var handler = new UpdateClientCommandHandler(context, dateProvider);
        var command = new UpdateClientCommand(client.Id, "Jane", "Smith", "456", "jane@test.com", "999", "Street 2", ClientStatus.Inactive);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Clients.FindAsync(client.Id);
        updated!.FirstName.Should().Be("Jane");
        updated.UpdateAt.Should().Be(dateProvider.UtcNow);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_WhenClientNotFound()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();
        var handler = new UpdateClientCommandHandler(context, dateProvider);
        var id = Guid.NewGuid();
        var command = new UpdateClientCommand(id, "Jane", "Doe", "123", "jane@test.com", "000", "Address", ClientStatus.Active);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClientErrors.NotFound(id));
    }
}
