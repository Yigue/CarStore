using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using SharedKernel;

namespace DomainTests;

/// <summary>
/// A quote used to belong to exactly one party: a Client XOR a Lead. Converting a lead into a
/// client therefore had to CLEAR the quote's LeadId to keep that invariant — and with it went
/// the only direct trace from the deal back to the enquiry that started it. Everything
/// downstream then had to rediscover the lead through Client.OriginLeadId, and anything that
/// forgot to (or hit a client created before the lead existed) simply lost the connection.
///
/// A converted lead and the client it became are the same person. The quote belongs to both.
/// </summary>
public class QuoteTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static Car MakeCar()
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        return new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, "QTA001", "desc", 15000m, DateTime.UtcNow);
    }

    private static Client MakeClient() =>
        new(DealerId, "Ada", "Lovelace", "111", "ada@test.com", "555", "Addr", DateTime.UtcNow);

    private static Lead MakeLead() =>
        Lead.Create(DealerId, "Pepe Mujica", "pepe@test.com", "555", LeadSource.Web, DateTime.UtcNow);

    private static Quote MakeQuote(Client? client, Lead? lead) =>
        new(DealerId, MakeCar(), client, lead, 14000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(7), "c", DateTime.UtcNow);

    [Fact]
    public void Constructor_Should_Throw_WhenNeitherPartyIsGiven()
    {
        var act = () => MakeQuote(null, null);

        act.Should().Throw<DomainException>().WithMessage("*either a Client or a Lead*");
    }

    [Fact]
    public void Constructor_Should_Accept_BothPartiesAtOnce()
    {
        var client = MakeClient();
        var lead = MakeLead();

        var quote = MakeQuote(client, lead);

        quote.ClientId.Should().Be(client.Id);
        quote.LeadId.Should().Be(lead.Id);
    }

    [Fact]
    public void Constructor_Should_LinkOnlyTheClient_WhenNoLeadIsGiven()
    {
        var client = MakeClient();

        var quote = MakeQuote(client, null);

        quote.ClientId.Should().Be(client.Id);
        quote.LeadId.Should().BeNull();
    }

    [Fact]
    public void AssignClient_Should_KeepTheLeadLink()
    {
        var lead = MakeLead();
        var quote = MakeQuote(null, lead);
        var clientId = Guid.NewGuid();

        quote.AssignClient(clientId);

        quote.ClientId.Should().Be(clientId);
        quote.LeadId.Should().Be(lead.Id, "converting a lead does not erase where the deal came from");
    }

    [Fact]
    public void AssignLead_Should_KeepTheClientLink()
    {
        var client = MakeClient();
        var quote = MakeQuote(client, null);
        var leadId = Guid.NewGuid();

        quote.AssignLead(leadId);

        quote.LeadId.Should().Be(leadId);
        quote.ClientId.Should().Be(client.Id);
    }

    [Fact]
    public void AssignClient_Should_Throw_OnEmptyId()
    {
        var quote = MakeQuote(null, MakeLead());

        var act = () => quote.AssignClient(Guid.Empty);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void QuoteCreatedDomainEvent_Should_CarryTheLeadId_WhenTheQuoteIsLeadLinked()
    {
        var lead = MakeLead();

        var quote = MakeQuote(null, lead);

        var created = quote.DomainEvents.OfType<Domain.Quotes.Events.QuoteCreatedDomainEvent>().Single();
        created.QuoteId.Should().Be(quote.Id);
    }
}
