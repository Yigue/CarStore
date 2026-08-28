using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Abstractions.Tenancy;
using Application.Quotes.CreateInquiry;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Leads;
using Domain.Quotes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Quotes;

/// <summary>
/// A public enquiry must produce a Lead. It used to create a Client and anchor the Quote to it,
/// so nothing ever reached the CRM pipeline and the Client-at-Negociación automation never fired.
///
/// The endpoint is <c>AllowAnonymous</c>: when host-header resolution misses, no tenant is
/// resolved and the global query filters are disabled for the whole request, so every lookup in
/// the handler must scope by DealerId itself. TestApplicationDbContext mirrors that state — it
/// applies the entity configurations but not the global filters — so an unscoped query here sees
/// every dealer's rows.
/// </summary>
public class CreateInquiryCommandHandlerTests
{
    private const string SharedEmail = "shared@buyer.com";

    private static readonly Guid DealerA = Guid.NewGuid();
    private static readonly Guid DealerB = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static ICurrentTenantService TenantService(Guid? dealerId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.SetupGet(t => t.HasTenant).Returns(dealerId.HasValue);
        mock.SetupGet(t => t.DealerId).Returns(dealerId ?? Guid.Empty);
        return mock.Object;
    }

    private static IRoundRobinLeadAllocator Allocator(Guid? agentId = null)
    {
        var mock = new Mock<IRoundRobinLeadAllocator>();
        mock.Setup(a => a.AllocateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentId);
        return mock.Object;
    }

    private static CreateInquiryCommandHandler CreateHandler(
        TestApplicationDbContext context,
        Guid? tenantDealerId = null,
        Guid? agentId = null) =>
        new(context, new FakeDateTimeProvider(), TenantService(tenantDealerId), Allocator(agentId));

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context, Guid dealerId, string patente)
    {
        var marca = new Marca($"Peugeot-{patente}");
        var modelo = new Modelo($"208-{patente}", marca.Id);
        var car = new Car(
            dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, patente, "desc", 15000m, DateTime.UtcNow);

        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static CreateInquiryCommand BuildCommand(
        Guid? carId,
        string email = SharedEmail,
        string phone = "1122334455",
        string comments = "Me interesa este vehiculo") =>
        new(carId, "Ana", "Fernandez", email, phone, comments);

    // ─── The core change ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_CreateALead_NotAClient()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "LEA001");

        var result = await CreateHandler(context).Handle(BuildCommand(car.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Lead lead = await context.Leads.SingleAsync();
        lead.DealerId.Should().Be(DealerA);
        lead.Status.Should().Be(LeadStatus.Nuevo);
        lead.Source.Should().Be(LeadSource.Web);
        lead.InterestedVehicleId.Should().Be(car.Id);
        result.Value.Should().Be(lead.Id, "the endpoint now returns the lead id");

        context.Clients.Should().BeEmpty("the Client is created at Negociación, not on enquiry");
    }

    [Fact]
    public async Task Handle_Should_AnchorTheQuoteToTheLead_NotToAClient()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "QUO001");

        await CreateHandler(context).Handle(BuildCommand(car.Id), CancellationToken.None);

        Quote quote = await context.Quotes.SingleAsync();
        Lead lead = await context.Leads.SingleAsync();

        quote.LeadId.Should().Be(lead.Id);
        quote.ClientId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_AssignAnAgent_WhenOneIsAvailable()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "AGE001");
        var agentId = Guid.NewGuid();

        await CreateHandler(context, agentId: agentId).Handle(BuildCommand(car.Id), CancellationToken.None);

        (await context.Leads.SingleAsync()).AssignedAgentId.Should().Be(agentId);
    }

    /// <summary>
    /// All three public forms post an empty phone — ContactFormComponent labels the field
    /// "(Opcional)" to the visitor. Before the domain invariant was relaxed this threw.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_Should_Succeed_WhenTheVisitorLeftThePhoneBlank(string phone)
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "PHO001");

        var result = await CreateHandler(context)
            .Handle(BuildCommand(car.Id, phone: phone), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Leads.CountAsync()).Should().Be(1);
    }

    // ─── Deduplication ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_ReuseTheOpenLead_WhenTheSamePersonEnquiresAgain()
    {
        using var context = CreateContext();
        Car first = await SeedCarAsync(context, DealerA, "DUP001");
        Car second = await SeedCarAsync(context, DealerA, "DUP002");

        var handler = CreateHandler(context);
        await handler.Handle(BuildCommand(first.Id, comments: "primera"), CancellationToken.None);
        await handler.Handle(BuildCommand(second.Id, comments: "segunda"), CancellationToken.None);

        Lead lead = await context.Leads.SingleAsync();
        lead.Notes.Should().Contain("primera").And.Contain("segunda");
        (await context.Quotes.CountAsync()).Should().Be(2, "each enquiry still produces its quote");
    }

    [Fact]
    public async Task Handle_Should_CreateAFreshLead_WhenThePreviousOneIsClosed()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "CLO001");

        Lead closed = Lead.Create(
            DealerA, "Ana Fernandez", SharedEmail, "1", LeadSource.Web, DateTime.UtcNow);
        closed.ForceStatus(LeadStatus.Perdido);
        context.Leads.Add(closed);
        await context.SaveChangesAsync();

        await CreateHandler(context).Handle(BuildCommand(car.Id), CancellationToken.None);

        (await context.Leads.CountAsync()).Should().Be(2, "a closed lead does not absorb new interest");
    }

    // ─── Tenant scoping ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_NotReuseALeadBelongingToAnotherDealer()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, DealerA, "TEN001");

        context.Leads.Add(Lead.Create(
            DealerB, "Ana Fernandez", SharedEmail, "1", LeadSource.Web, DateTime.UtcNow));
        await context.SaveChangesAsync();

        await CreateHandler(context).Handle(BuildCommand(car.Id), CancellationToken.None);

        List<Lead> leads = await context.Leads.ToListAsync();
        leads.Should().HaveCount(2);
        leads.Should().ContainSingle(l => l.DealerId == DealerA);
    }

    // ─── General enquiries (no vehicle) ────────────────────────────────────────

    [Fact]
    public async Task Handle_Should_UseTheResolvedTenant_ForAGeneralEnquiry()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context, tenantDealerId: DealerA)
            .Handle(BuildCommand(carId: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Lead lead = await context.Leads.SingleAsync();
        lead.DealerId.Should().Be(DealerA);
        lead.InterestedVehicleId.Should().BeNull();
        context.Quotes.Should().BeEmpty("there is no vehicle to quote");
    }

    /// <summary>
    /// The previous handler answered a general enquiry by taking "the first configured dealer".
    /// CurrentTenantService documents that exact fallback as a production cross-tenant leak that
    /// was removed from tenant resolution; it must not survive here either. Failing loudly beats
    /// filing a stranger's enquiry under an arbitrary dealership.
    /// </summary>
    [Fact]
    public async Task Handle_Should_Fail_WhenNoTenantResolvesForAGeneralEnquiry()
    {
        using var context = CreateContext();

        context.DealerSettings.Add(
            new Domain.DealerSettings.DealerSettings(DealerB, "Otra concesionaria", "otra@dealer.com"));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context, tenantDealerId: null)
            .Handle(BuildCommand(carId: null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Dealer.NotResolved");
        context.Leads.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTheVehicleDoesNotExist()
    {
        using var context = CreateContext();

        var result = await CreateHandler(context)
            .Handle(BuildCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Car.NotFound");
    }
}
