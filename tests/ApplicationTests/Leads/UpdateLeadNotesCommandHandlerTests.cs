using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.UpdateNotes;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

public class UpdateLeadNotesCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldUpdateNotes_WhenLeadExists()
    {
        using var context = CreateContext();
        var lead = Lead.Create(Guid.NewGuid(), "Alice Smith", "alice@test.com", "1234567", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var handler = new UpdateLeadNotesCommandHandler(context);
        var command = new UpdateLeadNotesCommand(lead.Id, "First contact done, interested in sedan.");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Leads.FindAsync(lead.Id);
        updated!.Notes.Should().Be("First contact done, interested in sedan.");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenLeadNotFound()
    {
        using var context = CreateContext();
        var handler = new UpdateLeadNotesCommandHandler(context);
        var command = new UpdateLeadNotesCommand(Guid.NewGuid(), "some notes");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
