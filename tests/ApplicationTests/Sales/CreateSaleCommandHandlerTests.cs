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
using SharedKernel;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuotePaymentMethod = Domain.Quotes.Attributes.PaymentMethod;
using QuoteStatus = Domain.Quotes.Attributes.QuoteStatus;
using Domain.Clients.Attributes;

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
    public async Task Handle_Should_Fail_WhenCarAlreadyHasCompletedSale()
    {
        // Slice 7.1 RED: car has a Completed sale in context.Sales (even if ServiceCar is stubbed stale as Reservado).
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Focus", "SLD999", StatusServiceCar.Reservado);

        var existingCompletedSale = new Sale(
            Guid.NewGuid(), car.Id, client.Id, 10000m, PaymentMethod.Cash,
            "CN-OLD", "previous completed sale", DateTime.UtcNow);
        existingCompletedSale.Complete();
        context.Sales.Add(existingCompletedSale);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-NEW", "attempt second sale");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.AlreadySold(car.Id));
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

    // ── Con qué clientes se puede cerrar una venta ────────────────────────────────────────────
    //
    // La venta exigia ClientStatus.Active, y eso cerraba un ciclo sin salida: el cliente de un lead
    // en Negociacion nace Prospect (CreateClientFromLeadOnNegociacionHandler), la venta pedia Active,
    // y Active lo asigna ActivateClientOnSaleCompletedHandler DESPUES de completar una venta. Active
    // significa "ya compro" — pedirlo antes de la primera venta es exigir que el negocio haya
    // terminado para poder empezarlo.

    private static async Task<Result<Guid>> SellToClientWithStatusAsync(
        TestApplicationDbContext context,
        Action<Client> mutateClient,
        string patente)
    {
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", patente);
        mutateClient(client);
        await context.SaveChangesAsync();

        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, new FakeDateTimeProvider(), tenantService.Object);

        return await handler.Handle(
            new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-ST", "status test"),
            CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenClientIsProspect()
    {
        using var context = CreateContext();

        var result = await SellToClientWithStatusAsync(context, c => c.SetProspect(), "PRO001");

        result.IsSuccess.Should().BeTrue("selling is what turns a prospect into a client");
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenClientIsVip()
    {
        using var context = CreateContext();

        var result = await SellToClientWithStatusAsync(context, c => c.SetVIP(), "VIP001");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenClientIsLost()
    {
        using var context = CreateContext();

        var result = await SellToClientWithStatusAsync(context, c => c.MarkAsLost(), "LOS001");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Clients.Inactive");
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenClientIsInactive()
    {
        using var context = CreateContext();

        var result = await SellToClientWithStatusAsync(context, c => c.Deactivate(), "INA001");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Clients.Inactive");
    }

    // CAT-01: a sale registered as already settled takes the unit off the floor in the same
    // transaction. SaleCompletedCarStatusHandler still does it off the outbox, but that is a
    // background tick: until it runs, the public catalogue — which filters on exactly this
    // column — keeps offering a vehicle that has been sold.
    [Fact]
    public async Task Handle_Should_MarkTheCarSold_WhenTheSaleIsCreatedCompleted()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Fiat", "Cronos", "CMP001");
        await context.SaveChangesAsync();

        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, new FakeDateTimeProvider(), tenantService.Object);

        var result = await handler.Handle(
            new CreateSaleCommand(car.Id, client.Id, 10000m, PaymentMethod.Cash, "CN-1", "cash sale", Status: SaleStatus.Completed),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await context.Cars.AsNoTracking().FirstAsync(c => c.Id == car.Id);
        persisted.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }

    // A pending sale is still a draft: it holds the unit, it does not sell it.
    // ══ VEN-02 · a prospect cannot be invoiced ═══════════════════════════════════════════════
    //
    // A client the CRM invented when the lead reached Negociación has a name, an email and a
    // placeholder DNI. Enough to quote, nowhere near enough to bill.

    private static (Car car, Client client) SeedCarAndPlaceholderClient(
        TestApplicationDbContext context,
        string patente)
    {
        var marca = new Marca($"Marca{patente}");
        var modelo = new Modelo($"Modelo{patente}", marca.Id);
        var dealerId = Guid.NewGuid();
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, patente, "desc", 10000m, DateTime.UtcNow);

        // Exactly how CreateClientFromLeadOnNegociacionHandler builds one: TEMP DNI, no address.
        var client = new Client(dealerId, "Carlos", "Perez", $"TEMP{Guid.NewGuid():N}"[..20],
            $"{Guid.NewGuid():N}@test.com", "111", string.Empty, DateTime.UtcNow);
        client.SetProspect();

        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        return (car, client);
    }

    private static CreateSaleCommandHandler CreateSaleHandler(TestApplicationDbContext context)
    {
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        return new CreateSaleCommandHandler(context, new FakeDateTimeProvider(), tenantService.Object);
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTheClientIsStillAPlaceholderAndNoIdentityIsSupplied()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndPlaceholderClient(context, "PLH001");
        await context.SaveChangesAsync();

        var result = await CreateSaleHandler(context).Handle(
            new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-1", ""),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.ClientDataIncomplete");
    }

    [Fact]
    public async Task Handle_Should_CompleteTheClient_WhenTheSaleCarriesTheMissingIdentity()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndPlaceholderClient(context, "PLH002");
        await context.SaveChangesAsync();

        var result = await CreateSaleHandler(context).Handle(
            new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-1", "",
                ClientDni: "20333444", ClientAddress: "Av. Siempreviva 742"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await context.Clients.SingleAsync(c => c.Id == client.Id);
        persisted.DNI.Should().Be("20333444");
        persisted.Address.Should().Be("Av. Siempreviva 742");
        persisted.Status.Should().Be(ClientStatus.Active, "selling is what turns a prospect into a customer");
    }

    // A real document is never silently rewritten by a sale form.
    [Fact]
    public async Task Handle_Should_NotOverwriteARealDniSuppliedOnTheSale()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Ford", "Ka", "REA001");
        await context.SaveChangesAsync();

        var result = await CreateSaleHandler(context).Handle(
            new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN-1", "",
                ClientDni: "99999999", ClientAddress: "Otra dirección"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await context.Clients.SingleAsync(c => c.Id == client.Id);
        persisted.DNI.Should().NotBe("99999999", "the sale form is not the place to rewrite an identity document");
    }

    // ══ VEN-04 · the sale inherits what the accepted quote agreed ════════════════════════════

    private static async Task<(Car car, Client client, Quote quote)> SeedAcceptedQuoteAsync(
        TestApplicationDbContext context,
        string patente,
        decimal proposedPrice,
        QuotePaymentMethod quotePaymentMethod)
    {
        var (car, client) = SeedCarAndClient(context, "Fiat", "Uno", patente);
        await context.SaveChangesAsync();

        var quote = new Quote(car.DealerId, car, client, null, proposedPrice, quotePaymentMethod,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        quote.Accept(DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        return (car, client, quote);
    }

    [Fact]
    public async Task Handle_Should_InheritThePriceAndPaymentMethod_FromTheAcceptedQuote()
    {
        using var context = CreateContext();
        var (car, client, quote) = await SeedAcceptedQuoteAsync(
            context, "INH001", 123_456m, QuotePaymentMethod.Financiado);

        var result = await CreateSaleHandler(context).Handle(
            new CreateSaleCommand(car.Id, client.Id, null, null, "CN-1", "", QuoteId: quote.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var sale = await context.Sales.SingleAsync(s => s.Id == result.Value);
        sale.FinalPrice.Amount.Should().Be(123_456m, "re-typing the number is how a car gets invoiced at a price nobody promised");
        sale.PaymentMethod.Should().Be(PaymentMethod.BankTransfer, "Financiado maps to a transfer, not to Efectivo");
    }

    [Fact]
    public async Task Handle_Should_PreferTheExplicitPrice_OverTheQuotes()
    {
        using var context = CreateContext();
        var (car, client, quote) = await SeedAcceptedQuoteAsync(
            context, "INH002", 123_456m, QuotePaymentMethod.Contado);

        var result = await CreateSaleHandler(context).Handle(
            new CreateSaleCommand(car.Id, client.Id, 120_000m, PaymentMethod.CreditCard, "CN-1", "", QuoteId: quote.Id),
            CancellationToken.None);

        var sale = await context.Sales.SingleAsync(s => s.Id == result.Value);
        sale.FinalPrice.Amount.Should().Be(120_000m, "the closing price may legitimately differ from the offer");
        sale.PaymentMethod.Should().Be(PaymentMethod.CreditCard);
    }

    [Theory]
    [InlineData(QuotePaymentMethod.Contado, PaymentMethod.Cash)]
    [InlineData(QuotePaymentMethod.Financiado, PaymentMethod.BankTransfer)]
    [InlineData(QuotePaymentMethod.Permuta, PaymentMethod.Other)]
    [InlineData(QuotePaymentMethod.Mixto, PaymentMethod.Other)]
    public void MapQuotePaymentMethod_TranslatesIntentIntoSettlement(
        QuotePaymentMethod intent,
        PaymentMethod expected)
    {
        CreateSaleCommandHandler.MapQuotePaymentMethod(intent).Should().Be(expected);
    }

    // ══ VEN-03 · how the operation is paid ═══════════════════════════════════════════════════

    [Fact]
    public async Task Handle_Should_RecordThePaymentBreakdownAndThePaperwork()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Fiat", "Mobi", "PAY001");
        await context.SaveChangesAsync();

        var result = await CreateSaleHandler(context).Handle(
            new CreateSaleCommand(car.Id, client.Id, 100_000m, PaymentMethod.BankTransfer, "CN-1", "",
                DownPayment: 30_000m,
                TradeInCarId: Guid.NewGuid(),
                TradeInValue: 20_000m,
                FinancedAmount: 50_000m,
                InstallmentCount: 12,
                InstallmentAmount: 4_500m,
                FinancingEntity: "Banco Nación",
                InvoiceNumber: "A-0001-00001234",
                TransferFormNumber: "08-998877",
                RegistrationNumber: "PAT-2024-555",
                DeliveryDate: new DateTime(2024, 3, 15)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var sale = await context.Sales.SingleAsync(s => s.Id == result.Value);
        sale.DownPayment!.Amount.Should().Be(30_000m);
        sale.TradeInValue!.Amount.Should().Be(20_000m);
        sale.FinancedAmount!.Amount.Should().Be(50_000m);
        sale.InstallmentCount.Should().Be(12);
        sale.FinancingEntity.Should().Be("Banco Nación");
        sale.InvoiceNumber.Should().Be("A-0001-00001234");
        sale.RegistrationNumber.Should().Be("PAT-2024-555");
        sale.DeliveryDate.Should().Be(new DateTime(2024, 3, 15));
    }

    // The parts have to add up to the price, or nobody can collect against the number.
    [Fact]
    public async Task Handle_Should_Fail_WhenThePaymentBreakdownDoesNotAddUpToThePrice()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Fiat", "Cronos", "PAY002");
        await context.SaveChangesAsync();

        var result = await CreateSaleHandler(context).Handle(
            new CreateSaleCommand(car.Id, client.Id, 100_000m, PaymentMethod.BankTransfer, "CN-1", "",
                DownPayment: 10_000m,
                FinancedAmount: 50_000m,
                InstallmentCount: 12,
                InstallmentAmount: 4_500m),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.PaymentPartsDoNotMatchPrice");
        (await context.Sales.CountAsync()).Should().Be(0, "a sale nobody can collect against is not written");
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenFinancingHasNoInstallments()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Fiat", "Pulse", "PAY003");
        await context.SaveChangesAsync();

        var result = await CreateSaleHandler(context).Handle(
            new CreateSaleCommand(car.Id, client.Id, 100_000m, PaymentMethod.BankTransfer, "CN-1", "",
                DownPayment: 40_000m,
                FinancedAmount: 60_000m),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sales.FinancingWithoutInstallments");
    }

    // A cash sale recorded in one line has nothing to add here.
    [Fact]
    public async Task Handle_Should_Succeed_WhenNoPaymentBreakdownIsSupplied()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Fiat", "Argo", "PAY004");
        await context.SaveChangesAsync();

        var result = await CreateSaleHandler(context).Handle(
            new CreateSaleCommand(car.Id, client.Id, 100_000m, PaymentMethod.Cash, "CN-1", ""),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var sale = await context.Sales.SingleAsync(s => s.Id == result.Value);
        sale.DownPayment.Should().BeNull("no seña recorded is not a seña of zero");
        sale.FinancedAmount.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_OnlyReserveTheCar_WhenTheSaleIsCreatedPending()
    {
        using var context = CreateContext();
        var (car, client) = SeedCarAndClient(context, "Fiat", "Argo", "PND001");
        await context.SaveChangesAsync();

        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, new FakeDateTimeProvider(), tenantService.Object);

        await handler.Handle(
            new CreateSaleCommand(car.Id, client.Id, 10000m, PaymentMethod.Cash, "CN-2", "draft"),
            CancellationToken.None);

        var persisted = await context.Cars.AsNoTracking().FirstAsync(c => c.Id == car.Id);
        persisted.ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }
}
