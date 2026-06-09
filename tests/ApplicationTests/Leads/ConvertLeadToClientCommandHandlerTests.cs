using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Leads.Convert;
using Domain.Clients;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Leads;

public class ConvertLeadToClientCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldCreateClient_WhenLeadExists_AndNoExistingClient()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Carlos Perez", "carlos@test.com", "1112223", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var handler = new ConvertLeadToClientCommandHandler(context, dateProvider);
        var command = new ConvertLeadToClientCommand(lead.Id, "30111222", "Av Corrientes 1234");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var client = await context.Clients.FirstOrDefaultAsync(c => c.Email.Value == "carlos@test.com");
        client.Should().NotBeNull();
        client!.DNI.Should().Be("30111222");
    }

    [Fact]
    public async Task Handle_ShouldLinkExistingClient_WhenEmailAlreadyExists()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Ana Lopez", "ana@test.com", "4445556", LeadSource.Web, DateTime.UtcNow);
        var existingClient = new Client(dealerId, "Ana", "Lopez", "20444555", "ana@test.com", "4445556", "Some address", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var handler = new ConvertLeadToClientCommandHandler(context, dateProvider);
        var command = new ConvertLeadToClientCommand(lead.Id, "20444555", "Some address");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var clientCount = await context.Clients.CountAsync();
        clientCount.Should().Be(1); // No duplicate created
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenLeadNotFound()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();
        var handler = new ConvertLeadToClientCommandHandler(context, dateProvider);
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "12345678", "Some address");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
