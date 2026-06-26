using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Leads.Convert;
using Domain.Clients;
using Domain.Clients.Attributes;
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
        var command = new ConvertLeadToClientCommand(lead.Id, "30111222", "Av Corrientes 1234", ClientType.Individual);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var client = await context.Clients.FirstOrDefaultAsync(c => c.Email.Value == "carlos@test.com");
        client.Should().NotBeNull();
        client!.DNI.Should().Be("30111222");
        client.Type.Should().Be(ClientType.Individual);
    }

    [Fact]
    public async Task Handle_ShouldPersistCorporateType_WhenRequested()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "ACME S.A.", "acme@test.com", "1112223", LeadSource.Portal, DateTime.UtcNow);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var handler = new ConvertLeadToClientCommandHandler(context, dateProvider);
        var command = new ConvertLeadToClientCommand(lead.Id, "30711222", "Av Corrientes 1234", ClientType.Corporate);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var client = await context.Clients.FirstOrDefaultAsync(c => c.Email.Value == "acme@test.com");
        client.Should().NotBeNull();
        client!.Type.Should().Be(ClientType.Corporate, "the handler must persist the requested ClientType, not hardcode Individual");
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
        var command = new ConvertLeadToClientCommand(lead.Id, "20444555", "Some address", ClientType.Individual);

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
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "12345678", "Some address", ClientType.Individual);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    // Phase 4 RED: ConvertLead → LeadStatus.Ganado

    [Fact]
    public async Task Handle_ShouldAdvanceLeadToGanado_OnSuccessfulConversion()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Pedro Ramirez", "pedro@test.com", "5556667", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var handler = new ConvertLeadToClientCommandHandler(context, dateProvider);
        var command = new ConvertLeadToClientCommand(lead.Id, "29111333", "Av. Siempre Viva 742", ClientType.Individual);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updatedLead = await context.Leads.FindAsync(lead.Id);
        updatedLead!.Status.Should().Be(LeadStatus.Ganado, "converting a lead must advance its status to Ganado");
    }

    [Fact]
    public async Task Handle_ShouldNotThrow_WhenLeadIsAlreadyGanado()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Maria Sosa", "maria.sosa@test.com", "7778889", LeadSource.Web, DateTime.UtcNow);
        // Manually advance lead to Ganado via ForceStatus (same as quote-accept path)
        lead.ForceStatus(LeadStatus.Ganado);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var handler = new ConvertLeadToClientCommandHandler(context, dateProvider);
        var command = new ConvertLeadToClientCommand(lead.Id, "33444555", "Calle Falsa 123", ClientType.Individual);

        // Must not throw; idempotent with UpdateLeadStatusFromQuoteHandler
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync();

        var updatedLead = await context.Leads.FindAsync(lead.Id);
        updatedLead!.Status.Should().Be(LeadStatus.Ganado, "status must remain Ganado");
    }
}
