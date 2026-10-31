using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Leads.RegisterWhatsAppInboundMessage;
using Domain.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Leads.WhatsAppInboundMessage;

/// <summary>sfc C0.4 / RN-046: a real inbound WhatsApp message creates or appends a Lead, keyed by phone.</summary>
public class RegisterWhatsAppInboundMessageCommandHandlerTests
{
    private static readonly Guid DealerA = Guid.NewGuid();
    private const string FromPhone = "5491122334455";

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static IRoundRobinLeadAllocator Allocator(Guid? agentId = null)
    {
        var mock = new Mock<IRoundRobinLeadAllocator>();
        mock.Setup(a => a.AllocateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentId);
        return mock.Object;
    }

    private static RegisterWhatsAppInboundMessageCommandHandler CreateHandler(
        TestApplicationDbContext context, Guid? agentId = null) =>
        new(context, Allocator(agentId));

    [Fact]
    public async Task Handle_Should_CreateALead_OnFirstMessage()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context).Handle(
            new RegisterWhatsAppInboundMessageCommand(DealerA, FromPhone, "Hola, quiero info del Corolla", DateTime.UtcNow),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Lead lead = await context.Leads.SingleAsync();
        lead.DealerId.Should().Be(DealerA);
        lead.Phone.Should().Be(FromPhone);
        lead.Source.Should().Be(LeadSource.WhatsApp);
        lead.Notes.Should().Contain("Hola, quiero info del Corolla");

        LeadActivity activity = await context.LeadActivities.SingleAsync();
        activity.Type.Should().Be(LeadActivityType.NoteAdded);
    }

    [Fact]
    public async Task Handle_Should_AppendToTheSameLead_OnASecondMessage_FromTheSamePhone()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        await handler.Handle(new RegisterWhatsAppInboundMessageCommand(DealerA, FromPhone, "Primer mensaje", DateTime.UtcNow), CancellationToken.None);
        await handler.Handle(new RegisterWhatsAppInboundMessageCommand(DealerA, FromPhone, "Segundo mensaje", DateTime.UtcNow.AddMinutes(5)), CancellationToken.None);

        (await context.Leads.CountAsync()).Should().Be(1, "a returning contact must append, not duplicate");
        (await context.LeadActivities.CountAsync()).Should().Be(2);

        Lead lead = await context.Leads.SingleAsync();
        lead.Notes.Should().Contain("Primer mensaje");
        lead.Notes.Should().Contain("Segundo mensaje");
    }

    [Fact]
    public async Task Handle_Should_AssignAnAgent_WhenOneIsAvailable()
    {
        using var context = CreateContext();
        var agentId = Guid.NewGuid();

        await CreateHandler(context, agentId).Handle(
            new RegisterWhatsAppInboundMessageCommand(DealerA, FromPhone, "Hola", DateTime.UtcNow),
            CancellationToken.None);

        (await context.Leads.SingleAsync()).AssignedAgentId.Should().Be(agentId);
    }

    [Fact]
    public async Task Handle_Should_ScopeByDealer_WhenTwoDealersShareAContactPhone()
    {
        using var context = CreateContext();
        var dealerB = Guid.NewGuid();

        await CreateHandler(context).Handle(new RegisterWhatsAppInboundMessageCommand(DealerA, FromPhone, "Hola A", DateTime.UtcNow), CancellationToken.None);
        await CreateHandler(context).Handle(new RegisterWhatsAppInboundMessageCommand(dealerB, FromPhone, "Hola B", DateTime.UtcNow), CancellationToken.None);

        (await context.Leads.CountAsync()).Should().Be(2, "the same phone number across two dealers is two separate leads");
    }
}
