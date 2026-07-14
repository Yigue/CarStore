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
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Sales.Events;
using Microsoft.EntityFrameworkCore;
using Moq;

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
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var quoteId = Guid.NewGuid();
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
