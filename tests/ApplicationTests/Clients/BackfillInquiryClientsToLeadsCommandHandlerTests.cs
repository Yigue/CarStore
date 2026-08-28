using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Clients.Commands.BackfillInquiryClientsToLeads;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Clients;

/// <summary>
/// This backfill rewrites who owns existing quotes and retires client rows, so its selection has
/// to be exact. The tests pin both halves: the fingerprint that catches enquiry-made clients, and
/// the exclusions that keep real customers and newsletter subscribers out of it.
/// </summary>
public class BackfillInquiryClientsToLeadsCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static BackfillInquiryClientsToLeadsCommandHandler CreateHandler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);

        var user = new Mock<IUserContext>();
        user.SetupGet(u => u.UserId).Returns(ActorId);

        return new BackfillInquiryClientsToLeadsCommandHandler(
            context, tenant.Object, user.Object, new FakeDateTimeProvider());
    }

    /// <summary>Exactly what the pre-fix inquiry handler produced: empty DNI, empty address.</summary>
    private static Client InquiryClient(string email) =>
        new(DealerId, "Ana", "Fernandez", string.Empty, email, "1122334455", string.Empty, DateTime.UtcNow);

    /// <summary>What Newsletter/Subscribe.cs produces — must never be caught.</summary>
    private static Client NewsletterClient(string email) =>
        new(DealerId, "Newsletter", "Suscriptor", $"NL-{Guid.NewGuid().ToString()[..8]}",
            email, "N/A", "Suscripto via Web", DateTime.UtcNow);

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

    private static Quote QuoteFor(Car car, Client client) =>
        new(DealerId, car, client, null, 9000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "consulta web", DateTime.UtcNow);

    // ─── Guard rails ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_Refuse_WhenNeitherDryRunNorConfirmed()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context)
            .Handle(new BackfillInquiryClientsToLeadsCommand(DryRun: false, Confirmed: false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Backfill.NotConfirmed");
    }

    [Fact]
    public async Task DryRun_Should_CountWithoutWriting()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "DRY001");
        Client client = InquiryClient("dry@test.com");
        context.Clients.Add(client);
        context.Quotes.Add(QuoteFor(car, client));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context)
            .Handle(new BackfillInquiryClientsToLeadsCommand(DryRun: true, Confirmed: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AffectedRowCount.Should().Be(1);
        result.Value.ReassignedQuoteCount.Should().Be(1);

        context.Leads.Should().BeEmpty("a dry run never writes");
        (await context.Clients.SingleAsync()).IsDeleted.Should().BeFalse();
        (await context.Quotes.SingleAsync()).ClientId.Should().Be(client.Id);
    }

    // ─── The conversion ────────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_Should_CreateTheLead_ReassignTheQuote_AndRetireTheClient()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "APP001");
        Client client = InquiryClient("apply@test.com");
        context.Clients.Add(client);
        context.Quotes.Add(QuoteFor(car, client));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context)
            .Handle(new BackfillInquiryClientsToLeadsCommand(DryRun: false, Confirmed: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AffectedRowCount.Should().Be(1);

        var lead = await context.Leads.SingleAsync();
        lead.ClientName.Should().Be("Ana Fernandez");
        lead.InterestedVehicleId.Should().Be(car.Id, "the vehicle comes from the enquiry's own quote");

        Quote quote = await context.Quotes.SingleAsync();
        quote.LeadId.Should().Be(lead.Id);
        quote.ClientId.Should().BeNull();

        (await context.Clients.IgnoreQueryFilters().SingleAsync()).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Apply_Should_BeIdempotent()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "IDE001");
        Client client = InquiryClient("idem@test.com");
        context.Clients.Add(client);
        context.Quotes.Add(QuoteFor(car, client));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context);
        var command = new BackfillInquiryClientsToLeadsCommand(DryRun: false, Confirmed: true);

        await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        second.Value.AffectedRowCount.Should().Be(0);
        (await context.Leads.CountAsync()).Should().Be(1, "a second run must not duplicate leads");
    }

    // ─── Exclusions ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_Should_SpareAClientWithASale()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "SAL001");
        Client client = InquiryClient("buyer@test.com");
        context.Clients.Add(client);
        context.Sales.Add(new Sale(
            DealerId, car.Id, client.Id, 9000m, Domain.Financial.Attributes.PaymentMethod.Cash,
            contractNumber: "C-1", comments: string.Empty, saleDate: DateTime.UtcNow));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context)
            .Handle(new BackfillInquiryClientsToLeadsCommand(DryRun: false, Confirmed: true), CancellationToken.None);

        result.Value.AffectedRowCount.Should().Be(0);
        (await context.Clients.SingleAsync()).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Apply_Should_SpareAClientWithAnAcceptedQuote()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "ACC001");
        Client client = InquiryClient("accepted@test.com");
        Quote quote = QuoteFor(car, client);
        quote.Accept(DateTime.UtcNow);

        context.Clients.Add(client);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context)
            .Handle(new BackfillInquiryClientsToLeadsCommand(DryRun: false, Confirmed: true), CancellationToken.None);

        result.Value.AffectedRowCount.Should().Be(0);
    }

    [Fact]
    public async Task Apply_Should_SpareNewsletterSubscribers()
    {
        using var context = CreateContext();
        context.Clients.Add(NewsletterClient("subscriber@test.com"));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context)
            .Handle(new BackfillInquiryClientsToLeadsCommand(DryRun: false, Confirmed: true), CancellationToken.None);

        result.Value.AffectedRowCount.Should().Be(0);
        context.Leads.Should().BeEmpty();
    }

    [Fact]
    public async Task Apply_Should_SpareAClientCreatedFromALead()
    {
        using var context = CreateContext();

        // OriginLeadId set — this client already came through the CRM funnel.
        context.Clients.Add(new Client(
            DealerId, "Ya", "Convertido", string.Empty, "converted@test.com", "1", string.Empty,
            DateTime.UtcNow, ClientType.Individual, originLeadId: Guid.NewGuid()));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context)
            .Handle(new BackfillInquiryClientsToLeadsCommand(DryRun: false, Confirmed: true), CancellationToken.None);

        result.Value.AffectedRowCount.Should().Be(0);
    }

    [Fact]
    public async Task Apply_Should_WriteAnAuditRow_OnEveryInvocation()
    {
        using var context = CreateContext();

        await CreateHandler(context)
            .Handle(new BackfillInquiryClientsToLeadsCommand(DryRun: true, Confirmed: false), CancellationToken.None);

        var audit = await context.BackfillAudits.SingleAsync();
        audit.ActorUserId.Should().Be(ActorId);
        audit.Action.Should().Be(BackfillAction.DryRun);
    }
}
