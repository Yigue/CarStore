using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.Activity;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Leads;
using Domain.Leads.Events;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Quotes.Events;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

public class LeadActivityHandlersTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<Lead> SeedLeadAsync(TestApplicationDbContext context)
    {
        var lead = Lead.Create(
            DealerId, "Ana Fernandez", "ana@test.com", "1", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();
        return lead;
    }

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context, string patente)
    {
        var marca = new Marca($"Fiat-{patente}");
        var modelo = new Modelo($"Cronos-{patente}", marca.Id);
        var car = new Car(
            DealerId, marca, modelo, Color.Gray, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1300, 30000, 2021, patente, "desc",
            9000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static RecordLeadLifecycleActivityHandler Lifecycle(TestApplicationDbContext context) =>
        new(context, new LeadActivityRecorder(context), new FakeDateTimeProvider());

    private static RecordQuoteActivityOnLeadHandler QuoteHandler(TestApplicationDbContext context) =>
        new(context, new LeadActivityRecorder(context), new FakeDateTimeProvider());

    private static RecordSaleActivityOnLeadHandler SaleHandler(TestApplicationDbContext context) =>
        new(context, new LeadActivityRecorder(context), new FakeDateTimeProvider());

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusChange_Should_RecordBothStages()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);

        await Lifecycle(context).Handle(
            new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Nuevo, LeadStatus.Contactado),
            CancellationToken.None);

        LeadActivity entry = await context.LeadActivities.SingleAsync();
        entry.Type.Should().Be(LeadActivityType.StatusChanged);
        entry.Description.Should().Be("Estado: Nuevo → Contactado");
    }

    /// <summary>
    /// The loss reason was captured on the lead and rendered nowhere. Folding it into the timeline
    /// entry is what finally makes "why did we lose this?" answerable from the history.
    /// </summary>
    [Fact]
    public async Task StatusChangeToPerdido_Should_IncludeTheLossReason()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);
        lead.UpdateStatus(LeadStatus.Contactado, "contactado");
        lead.UpdateStatus(LeadStatus.Perdido, null, LeadLossReason.ComproEnOtra);
        await context.SaveChangesAsync();

        await Lifecycle(context).Handle(
            new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Perdido),
            CancellationToken.None);

        (await context.LeadActivities.SingleAsync())
            .Description.Should().Be("Estado: Contactado → Perdido (motivo: compró en otra concesionaria)");
    }

    [Fact]
    public async Task AgentAssignment_Should_LinkTheAgent()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);
        var agentId = Guid.NewGuid();

        await Lifecycle(context).Handle(
            new LeadAssignedDomainEvent(lead.Id, agentId), CancellationToken.None);

        LeadActivity entry = await context.LeadActivities.SingleAsync();
        entry.RelatedEntityId.Should().Be(agentId);
        entry.RelatedEntityType.Should().Be("User");
    }

    [Fact]
    public async Task UnknownLead_Should_BeIgnored_NotThrow()
    {
        using var context = CreateContext();

        Func<Task> act = () => Lifecycle(context).Handle(
            new LeadCreatedDomainEvent(Guid.NewGuid(), null), CancellationToken.None);

        await act.Should().NotThrowAsync();
        context.LeadActivities.Should().BeEmpty();
    }

    // ─── Quotes ────────────────────────────────────────────────────────────────

    /// <summary>
    /// QuoteRejectedDomainEvent had no subscriber at all — its twin had two — so a rejection and
    /// its reason vanished the moment it happened.
    /// </summary>
    [Fact]
    public async Task QuoteRejection_Should_ReachTheLeadTimeline_WithItsReason()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);
        Car car = await SeedCarAsync(context, "REJ001");

        var quote = new Quote(DealerId, car, null, lead, 9000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        await QuoteHandler(context).Handle(
            new QuoteRejectedDomainEvent(quote.Id, "precio fuera de presupuesto"),
            CancellationToken.None);

        LeadActivity entry = await context.LeadActivities.SingleAsync();
        entry.Type.Should().Be(LeadActivityType.QuoteRejected);
        entry.Description.Should().Contain("precio fuera de presupuesto");
        entry.RelatedEntityId.Should().Be(quote.Id);
    }

    /// <summary>
    /// REQ-2.2 supersedes the earlier expectation that this entry announce a jump to Ganado.
    /// Acceptance stopped closing the deal, so an entry still saying it did would be the timeline
    /// telling the agent the work is finished while the sale is not even recorded. What the entry
    /// owes now is the next step.
    /// </summary>
    [Fact]
    public async Task QuoteAcceptance_Should_NameTheStepStillOwed()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);
        Car car = await SeedCarAsync(context, "ACC001");

        var quote = new Quote(DealerId, car, null, lead, 9000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        await QuoteHandler(context).Handle(
            new QuoteAcceptedDomainEvent(quote.Id), CancellationToken.None);

        LeadActivity entry = await context.LeadActivities.SingleAsync();
        entry.Description.Should().Contain(
            "registrá la venta",
            "the timeline must point at the sale that is still missing");
        entry.Description.Should().NotContain(
            "Ganado",
            "announcing a stage the lead did not reach is worse than saying nothing");
    }

    /// <summary>A quote raised before enquiries created leads hangs off a client instead.</summary>
    [Fact]
    public async Task QuoteOnALegacyClient_Should_ReachTheOriginLead()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);
        Car car = await SeedCarAsync(context, "LEG001");

        var client = new Client(DealerId, "Ana", "Fernandez", "1", "ana@test.com", "2", "Addr",
            DateTime.UtcNow, Domain.Clients.Attributes.ClientType.Individual, originLeadId: lead.Id);
        var quote = new Quote(DealerId, car, client, null, 9000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Clients.Add(client);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        await QuoteHandler(context).Handle(
            new QuoteRejectedDomainEvent(quote.Id, "no le gustó"), CancellationToken.None);

        (await context.LeadActivities.SingleAsync()).LeadId.Should().Be(lead.Id);
    }

    // ─── Idempotency ───────────────────────────────────────────────────────────

    /// <summary>
    /// These handlers run off the outbox, which retries. A redelivered message must not grow the
    /// history a second time — duplicated entries read as real repeated activity, which is worse
    /// than no entry at all because it looks plausible.
    /// </summary>
    [Fact]
    public async Task Redelivery_Should_NotDuplicateTheEntry()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);
        Car car = await SeedCarAsync(context, "IDE001");

        var quote = new Quote(DealerId, car, null, lead, 9000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = QuoteHandler(context);
        var notification = new QuoteRejectedDomainEvent(quote.Id, "motivo");

        await handler.Handle(notification, CancellationToken.None);
        await handler.Handle(notification, CancellationToken.None);

        (await context.LeadActivities.CountAsync()).Should().Be(1);
    }

    // ─── Sales ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaleRegistered_Should_CloseTheLoopOnTheLead()
    {
        using var context = CreateContext();
        Lead lead = await SeedLeadAsync(context);
        var saleId = Guid.NewGuid();

        await SaleHandler(context).Handle(
            new SaleCreatedDomainEvent(saleId, Guid.NewGuid(), Guid.NewGuid(), new Money(15000m), lead.Id),
            CancellationToken.None);

        LeadActivity entry = await context.LeadActivities.SingleAsync();
        entry.Type.Should().Be(LeadActivityType.SaleRegistered);
        entry.RelatedEntityId.Should().Be(saleId);
        entry.RelatedEntityType.Should().Be("Sale");
    }
}
