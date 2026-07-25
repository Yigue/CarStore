using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Abstractions.Tenancy;
using Application.Sales.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Sales.Events;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuotePaymentMethod = Domain.Quotes.Attributes.PaymentMethod;
using QuoteStatus = Domain.Quotes.Attributes.QuoteStatus;

namespace Application.UnitTests.Sales;

public class CreateSaleCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static (Car car, Client client) SeedCarAndClient(
        TestApplicationDbContext context,
        string marcaName,
        string modeloName,
        string patente,
        StatusServiceCar carStatus = StatusServiceCar.Disponible)
    {
        var marca = new Marca(marcaName);
        var modelo = new Modelo(modeloName, marca.Id);
        var dealerId = Guid.NewGuid();
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, carStatus, 4, 5, 1600, 1000, 2020, patente, "desc", 10000m, DateTime.UtcNow);
        var client = new Client(dealerId, "Alice", "Johnson", Guid.NewGuid().ToString("N")[..8], $"{Guid.NewGuid():N}@test.com", "111", "Addr1", DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        return (car, client);
    }

    [Fact]
    public async Task Handle_Should_CreateSale_WhenDataIsValid()
    {
        // Explicitly requests Completed: exercises the "sale settled immediately" path
        // (e.g. a cash sale recorded as already closed).
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Fiesta", "XYZ789");
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN123", "ok", Status: SaleStatus.Completed);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Sales.Should().ContainSingle(s => s.Id == result.Value);
        (await context.Cars.FindAsync(car.Id))!.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }

    [Fact]
    public async Task Handle_Should_LeaveSalePending_And_ReserveCar_WhenStatusNotProvided()
    {
        // Bug 1: sales used to be force-completed at creation regardless of the
        // caller's intent. The default (no Status) must now leave the sale Pending
        // and reserve the car (not mark it Vendido).
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Renault", "Sandero", "PND001");
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-PND", "pending sale");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.FirstAsync(s => s.Id == result.Value);
        sale.Status.Should().Be(SaleStatus.Pending);
        (await context.Cars.FindAsync(car.Id))!.ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }

    [Fact]
    public async Task Handle_Should_CompleteSale_And_RaiseDomainEvent_WhenStatusIsCompleted()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Toyota", "Corolla", "CMP001");
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-CMP", "completed sale", Status: SaleStatus.Completed);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.FirstAsync(s => s.Id == result.Value);
        sale.Status.Should().Be(SaleStatus.Completed);
        sale.DomainEvents.Should().Contain(e => e is SaleCompletedDomainEvent);
        (await context.Cars.FindAsync(car.Id))!.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }

    [Fact]
    public async Task Handle_Should_KeepCarReservado_WhenPendingSale_ConvertsAlreadyReservedCar()
    {
        // D-1: a car reserved by an accepted quote stays Reservado when the
        // resulting sale is created as Pending (Reserve() would otherwise throw
        // because it requires the car to currently be Disponible).
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Peugeot", "208", "RSV001", StatusServiceCar.Reservado);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-RSV", "from quote");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Cars.FindAsync(car.Id))!.ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }

    [Fact]
    public async Task Handle_Should_PersistQuoteId_When_Provided()
    {
        using var context = CreateContext();
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var dealerId = Guid.NewGuid();
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, "QTE001", "desc", 15000m, DateTime.UtcNow);
        var client = new Client(dealerId, "Carol", "White", "456", "carol@test.com", "222", "Addr2", DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        var quote = BuildQuote(car, client, null, QuoteStatus.Accepted, dealerId);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var quoteId = quote.Id;
        var command = new CreateSaleCommand(car.Id, client.Id, 14000m, PaymentMethod.Cash, "CN-Q", "from quote", LeadId: null, QuoteId: quoteId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.FirstAsync(s => s.Id == result.Value);
        sale.QuoteId.Should().Be(quoteId);
    }

    [Fact]
    public async Task Handle_Should_LeaveQuoteIdNull_When_NotProvided()
    {
        using var context = CreateContext();
        var marca = new Marca("Fiat");
        var modelo = new Modelo("Cronos", marca.Id);
        var dealerId = Guid.NewGuid();
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1400, 1000, 2022, "QTE002", "desc", 12000m, DateTime.UtcNow);
        var client = new Client(dealerId, "Dan", "Green", "457", "dan@test.com", "333", "Addr3", DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 11000m, PaymentMethod.Cash, "CN-NQ", "no quote");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.FirstAsync(s => s.Id == result.Value);
        sale.QuoteId.Should().BeNull();
    }

    private static Quote BuildQuote(
        Car car,
        Client? client,
        Lead? lead,
        QuoteStatus status,
        Guid dealerId)
    {
        var quote = new Quote(
            dealerId,
            car,
            client,
            lead,
            car.Price.Amount,
            QuotePaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30),
            "quote comments",
            DateTime.UtcNow);

        if (status == QuoteStatus.Accepted)
        {
            quote.Accept(DateTime.UtcNow);
        }

        return quote;
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenQuoteNotFound()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", "QNF001");
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var quoteId = Guid.NewGuid();
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-QNF", "no quote found", LeadId: null, QuoteId: quoteId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.QuoteNotFound(quoteId));
        context.Sales.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenQuoteNotAccepted()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", "QNA001");
        var dealerId = Guid.NewGuid();
        var quote = BuildQuote(car, client, null, QuoteStatus.Pending, dealerId);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-QNA", "not accepted", LeadId: null, QuoteId: quote.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.QuoteNotAccepted(quote.Id));
        context.Sales.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenQuoteCarMismatch()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", "QCM001");
        var (otherCar, _) = SeedCarAndClient(context, "Fiat", "Uno", "QCM002");
        var dealerId = Guid.NewGuid();
        var quote = BuildQuote(otherCar, client, null, QuoteStatus.Accepted, dealerId);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-QCM", "car mismatch", LeadId: null, QuoteId: quote.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.QuoteMismatch(quote.Id));
        context.Sales.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenQuoteClientMismatch()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", "QLM001");
        var otherClient = new Client(Guid.NewGuid(), "Other", "Client", "999888", "other@test.com", "555", "Addr9", DateTime.UtcNow);
        context.Clients.Add(otherClient);
        var dealerId = Guid.NewGuid();
        var quote = BuildQuote(car, otherClient, null, QuoteStatus.Accepted, dealerId);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-QLM", "client mismatch", LeadId: null, QuoteId: quote.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.QuoteMismatch(quote.Id));
        context.Sales.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenLeadLinkedQuoteMatchesOnLeadId()
    {
        // Lead-linked quotes match party on LeadId only (design.md ADR-6, §10 accepted
        // risk: the sale's ClientId is not cross-checked against the lead's converted
        // client here — the FE selector never offers lead-linked quotes, and full
        // client derivation is deferred). This test pins that documented behavior.
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", "QLL001");
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Lead Uno", "lead1@test.com", "555-1", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);
        var quote = BuildQuote(car, null, lead, QuoteStatus.Accepted, dealerId);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-QLL", "lead-linked matches", LeadId: lead.Id, QuoteId: quote.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.FirstAsync(s => s.Id == result.Value);
        sale.QuoteId.Should().Be(quote.Id);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenLeadLinkedQuoteLeadMismatch()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", "QLM002");
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Lead Dos", "lead2@test.com", "555-2", LeadSource.Web, DateTime.UtcNow);
        var otherLead = Lead.Create(dealerId, "Lead Tres", "lead3@test.com", "555-3", LeadSource.Web, DateTime.UtcNow);
        context.Leads.AddRange(lead, otherLead);
        var quote = BuildQuote(car, null, lead, QuoteStatus.Accepted, dealerId);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-QLM2", "lead mismatch", LeadId: otherLead.Id, QuoteId: quote.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.QuoteMismatch(quote.Id));
        context.Sales.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenQuoteMatches()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", "QOK001");
        var dealerId = Guid.NewGuid();
        var quote = BuildQuote(car, client, null, QuoteStatus.Accepted, dealerId);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-QOK", "matches", LeadId: null, QuoteId: quote.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.FirstAsync(s => s.Id == result.Value);
        sale.QuoteId.Should().Be(quote.Id);
    }

    [Fact]
    public async Task Handle_Should_Ignore_QuoteGuard_WhenQuoteIdNull()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", "QNL001");
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-QNULL", "no quote at all");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.FirstAsync(s => s.Id == result.Value);
        sale.QuoteId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Car_NotFound()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, "Bob", "Brown", "123", "bob@test.com", "111", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var carId = Guid.NewGuid();
        var command = new CreateSaleCommand(carId, client.Id, 1000m, PaymentMethod.Cash, "C", "comment");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.NotFound(carId));
        context.Sales.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_PersistSalespersonId_When_Provided()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Chevrolet", "Onix", "SLP001");
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var salespersonId = Guid.NewGuid();
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-SLP", "with salesperson", SalespersonId: salespersonId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.FirstAsync(s => s.Id == result.Value);
        sale.SalespersonId.Should().Be(salespersonId);
    }

    [Fact]
    public async Task Handle_Should_LeaveSalespersonIdNull_When_NotProvided()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Chevrolet", "Tracker", "SLP002");
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-NOSLP", "no salesperson");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.FirstAsync(s => s.Id == result.Value);
        sale.SalespersonId.Should().BeNull();
    }
}
