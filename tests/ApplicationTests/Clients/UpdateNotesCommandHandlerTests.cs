using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Clients.UpdateNotes;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Clients;

/// <summary>
/// PR1 RED→GREEN: UpdateNotesCommandHandler invariants.
/// </summary>
public class UpdateNotesCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Client SeedClient(TestApplicationDbContext context, Guid? dealerId = null)
    {
        var d = dealerId ?? Guid.NewGuid();
        var client = new Client(d, "Ana", "Lopez", "20444555", "ana@test.com", "111", "Street 1", DateTime.UtcNow);
        context.Clients.Add(client);
        context.SaveChanges();
        client.ClearDomainEvents();
        return client;
    }

    [Fact]
    public async Task Handle_Should_Update_Notes_And_Return_Success()
    {
        using var context = CreateContext();
        var client = SeedClient(context);
        var actorId = Guid.NewGuid();
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc) };

        var handler = new UpdateNotesCommandHandler(context, dateProvider);
        var command = new UpdateNotesCommand(client.Id, "New notes text", actorId);

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Clients.FindAsync(client.Id);
        updated!.Notes.Should().Be("New notes text");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Client_Not_Found()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();

        var handler = new UpdateNotesCommandHandler(context, dateProvider);
        var command = new UpdateNotesCommand(Guid.NewGuid(), "Notes", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Notes_Exceed_2000_Chars()
    {
        using var context = CreateContext();
        var client = SeedClient(context);
        var dateProvider = new FakeDateTimeProvider();

        var handler = new UpdateNotesCommandHandler(context, dateProvider);
        var command = new UpdateNotesCommand(client.Id, new string('x', 2001), Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Allow_Null_Notes_To_Clear_Field()
    {
        using var context = CreateContext();
        var client = SeedClient(context);
        var dateProvider = new FakeDateTimeProvider();

        var handler = new UpdateNotesCommandHandler(context, dateProvider);
        var command = new UpdateNotesCommand(client.Id, null, Guid.NewGuid());

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Clients.FindAsync(client.Id);
        updated!.Notes.Should().BeNull();
    }
}
