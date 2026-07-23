using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Abstractions.Tenancy;
using Application.Quotes.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Quotes;

public class CreateQuoteCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static ICurrentTenantService CreateTenantService()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(DealerId);
        return tenantMock.Object;
    }

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context)
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, "DEL001", "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static async Task<Client> SeedClientAsync(TestApplicationDbContext context, ClientStatus status)
    {
        var client = new Client(DealerId, "Eve", "Black", "999", "eve@test.com", "444", "Addr", DateTime.UtcNow);
        if (status == ClientStatus.Lost)
        {
            client.MarkAsLost();
        }
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return client;
    }

    private static async Task<Lead> SeedLeadAsync(TestApplicationDbContext context, LeadStatus status, Guid? dealerId = null)
    {
        var lead = Lead.Create(dealerId ?? DealerId, "John Doe", "john@test.com", "555", LeadSource.Web, DateTime.UtcNow);
        if (status != LeadStatus.Nuevo)
        {
            lead.ForceStatus(status);
        }
        context.Leads.Add(lead);
        await context.SaveChangesAsync();
        return lead;
    }

    private static CreateQuoteCommand BuildCommand(Guid carId, Guid? clientId, Guid? leadId) => new(
        carId,
        clientId,
        leadId,
        14000m,
        PaymentMethod.Contado,
        DateTime.UtcNow.AddDays(7),
        "comments");

    [Fact]
    public async Task Handle_Should_Fail_WhenClientIsLost()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var client = await SeedClientAsync(context, ClientStatus.Lost);
        var handler = new CreateQuoteCommandHandler(context, new FakeDateTimeProvider(), CreateTenantService());

        var result = await handler.Handle(BuildCommand(car.Id, client.Id, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(QuoteErrors.ClientNotQuotable(client.Id));
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenLeadIsPerdido()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var lead = await SeedLeadAsync(context, LeadStatus.Perdido);
        var handler = new CreateQuoteCommandHandler(context, new FakeDateTimeProvider(), CreateTenantService());

        var result = await handler.Handle(BuildCommand(car.Id, null, lead.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(QuoteErrors.LeadNotQuotable(lead.Id));
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenLeadIsArchivado()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var lead = await SeedLeadAsync(context, LeadStatus.Archivado);
        var handler = new CreateQuoteCommandHandler(context, new FakeDateTimeProvider(), CreateTenantService());

        var result = await handler.Handle(BuildCommand(car.Id, null, lead.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(QuoteErrors.LeadNotQuotable(lead.Id));
    }

    [Fact]
    public async Task Handle_Should_NotReserveCar_WhenGateFails()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var client = await SeedClientAsync(context, ClientStatus.Lost);
        var handler = new CreateQuoteCommandHandler(context, new FakeDateTimeProvider(), CreateTenantService());

        var result = await handler.Handle(BuildCommand(car.Id, client.Id, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        var persistedCar = await context.Cars.SingleAsync(c => c.Id == car.Id);
        persistedCar.ServiceCar.Should().Be(StatusServiceCar.Disponible);
        (await context.Quotes.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenClientActive()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var client = await SeedClientAsync(context, ClientStatus.Active);
        var handler = new CreateQuoteCommandHandler(context, new FakeDateTimeProvider(), CreateTenantService());

        var result = await handler.Handle(BuildCommand(car.Id, client.Id, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persistedCar = await context.Cars.SingleAsync(c => c.Id == car.Id);
        persistedCar.ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenLeadNegociacion()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var lead = await SeedLeadAsync(context, LeadStatus.Negociacion);
        var handler = new CreateQuoteCommandHandler(context, new FakeDateTimeProvider(), CreateTenantService());

        var result = await handler.Handle(BuildCommand(car.Id, null, lead.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persistedCar = await context.Cars.SingleAsync(c => c.Id == car.Id);
        persistedCar.ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }

    // Regression for CRITICAL-1 (verify-report crm-cotizaciones-etapa3): the Lead
    // lookup used .IgnoreQueryFilters() to bypass the (in production, dead)
    // Archivado-hiding filter, but that call ALSO strips the tenant filter, so a
    // leadId belonging to another dealer resolved successfully and got linked into
    // a persisted Quote. The fix scopes the query to the current tenant explicitly,
    // independent of which EF Core global query filter is actually active.
    [Fact]
    public async Task Handle_Should_ReturnLeadNotFound_WhenLeadBelongsToDifferentDealer()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var otherDealerId = Guid.NewGuid();
        var foreignLead = await SeedLeadAsync(context, LeadStatus.Negociacion, otherDealerId);
        var handler = new CreateQuoteCommandHandler(context, new FakeDateTimeProvider(), CreateTenantService());

        var result = await handler.Handle(BuildCommand(car.Id, null, foreignLead.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(LeadErrors.NotFound(foreignLead.Id));
        (await context.Quotes.AnyAsync()).Should().BeFalse();
        var persistedCar = await context.Cars.SingleAsync(c => c.Id == car.Id);
        persistedCar.ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }
}
