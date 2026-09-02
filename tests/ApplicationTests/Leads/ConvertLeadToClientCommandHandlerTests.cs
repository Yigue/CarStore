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

    // REQ-2.3 supersedes the earlier "Phase 4" requirement that conversion advance the lead to
    // Ganado. That rule made this handler the fourth writer of a stage it could not justify: a
    // person can be registered as a client for reasons that have nothing to do with having
    // bought, and every one of those conversions closed a deal with no sale behind it. Ganado now
    // follows a sale and nothing else (REQ-2.1). The assertion below is the old one inverted, on
    // purpose — it is what would catch the ForceStatus being put back.

    [Fact]
    public async Task Handle_ShouldLeaveTheStageAlone_OnSuccessfulConversion()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Pedro Ramirez", "pedro@test.com", "5556667", LeadSource.Web, DateTime.UtcNow);
        lead.ForceStatus(LeadStatus.Negociacion);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var handler = new ConvertLeadToClientCommandHandler(context, dateProvider);
        var command = new ConvertLeadToClientCommand(lead.Id, "29111333", "Av. Siempre Viva 742", ClientType.Individual);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updatedLead = await context.Leads.FindAsync(lead.Id);
        updatedLead!.Status.Should().Be(
            LeadStatus.Negociacion,
            "creating the client record says nothing about whether the deal closed; only a sale moves the lead to Ganado");
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

        // Converting a lead that a sale already closed must neither throw nor drag it backwards.
        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync();

        var updatedLead = await context.Leads.FindAsync(lead.Id);
        updatedLead!.Status.Should().Be(LeadStatus.Ganado, "status must remain Ganado");
    }

    // REQ-CRM-DEDUP-001 / ADR-4: ConvertedClientId-first dedup + inline Activate() (ADR-2).

    [Fact]
    public async Task Handle_LeadAlreadyHasProspectClient_ReusesItAndActivates()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Lucia Fernandez", "lucia@test.com", "6667778", LeadSource.Web, DateTime.UtcNow);
        var prospectClient = new Client(dealerId, "Lucia", "Fernandez", "TEMP0002", "lucia@test.com", "6667778", string.Empty, DateTime.UtcNow, ClientType.Individual, lead.Id);
        prospectClient.SetProspect();
        context.Leads.Add(lead);
        context.Clients.Add(prospectClient);
        await context.SaveChangesAsync();

        lead.MarkConverted(prospectClient.Id);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var handler = new ConvertLeadToClientCommandHandler(context, dateProvider);
        var command = new ConvertLeadToClientCommand(lead.Id, "20666777", "Some address", ClientType.Individual);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var clientCount = await context.Clients.CountAsync();
        clientCount.Should().Be(1, "the Prospect Client created at Negociación must be reused, not duplicated");

        var reused = await context.Clients.SingleAsync();
        reused.Id.Should().Be(prospectClient.Id);
        reused.Status.Should().Be(ClientStatus.Active, "reusing the Prospect Client must Activate() it (ADR-2)");
    }

    // ── The client that already existed ───────────────────────────────────────────────────────
    //
    // When conversion reuses a client rather than creating one, nothing used to stamp
    // OriginLeadId on it. Every rule that walks back from a client to the enquiry it came from
    // — the pipeline's HasQuoteAsync, HasSaleAsync, the outbox's lead resolution — then found
    // nothing, and the lead was stuck one stage behind with no way to tell why.

    [Fact]
    public async Task Handle_Should_StampOriginLead_OnAReusedClient()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Pepe Mujica", "pepe@test.com", "1112223", LeadSource.Web, DateTime.UtcNow);
        var existing = new Client(dealerId, "Pepe", "Mujica", "30111222", "pepe@test.com", "555", "Addr", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(existing);
        await context.SaveChangesAsync();

        var handler = new ConvertLeadToClientCommandHandler(context, new FakeDateTimeProvider());
        var result = await handler.Handle(
            new ConvertLeadToClientCommand(lead.Id, "30111222", "Av Corrientes 1234", ClientType.Individual),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var reused = await context.Clients.SingleAsync(c => c.Id == existing.Id);
        reused.OriginLeadId.Should().Be(lead.Id);
    }

    [Fact]
    public async Task Handle_Should_NotReuseAClientFromAnotherDealer()
    {
        // Sharing an email address across dealerships is normal — one person shops at several.
        // Matching on email alone would hand this lead the other dealership's customer record.
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var otherDealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Pepe Mujica", "pepe@test.com", "1112223", LeadSource.Web, DateTime.UtcNow);
        var foreign = new Client(otherDealerId, "Pepe", "Mujica", "30111222", "pepe@test.com", "555", "Addr", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(foreign);
        await context.SaveChangesAsync();

        var handler = new ConvertLeadToClientCommandHandler(context, new FakeDateTimeProvider());
        var result = await handler.Handle(
            new ConvertLeadToClientCommand(lead.Id, "30111222", "Av Corrientes 1234", ClientType.Individual),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(foreign.Id, "a lead must never be linked to another dealership's client");
        var created = await context.Clients.SingleAsync(c => c.Id == result.Value);
        created.DealerId.Should().Be(dealerId);
    }

    [Fact]
    public async Task Handle_Should_KeepTheLeadLinkOnReassignedQuotes()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Pepe Mujica", "pepe@test.com", "1112223", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);

        var marca = new Domain.Cars.Attributes.Marca("Peugeot");
        var modelo = new Domain.Cars.Attributes.Modelo("208", marca.Id);
        var car = new Domain.Cars.Car(dealerId, marca, modelo, Domain.Cars.Attributes.Color.Red,
            Domain.Cars.Attributes.TypeCar.Sedan, Domain.Cars.Attributes.StatusCar.New,
            Domain.Cars.Attributes.StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, "CNV001", "d", 15000m, DateTime.UtcNow);
        var quote = new Domain.Quotes.Quote(dealerId, car, null, lead, 14000m,
            Domain.Quotes.Attributes.PaymentMethod.Contado, DateTime.UtcNow.AddDays(7), "c", DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = new ConvertLeadToClientCommandHandler(context, new FakeDateTimeProvider());
        var result = await handler.Handle(
            new ConvertLeadToClientCommand(lead.Id, "30111222", "Av Corrientes 1234", ClientType.Individual),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await context.Quotes.SingleAsync(q => q.Id == quote.Id);
        persisted.ClientId.Should().Be(result.Value);
        persisted.LeadId.Should().Be(lead.Id, "the quote belongs to the person, on both sides of the conversion");
    }
}
