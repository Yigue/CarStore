using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Sales.Create;
using Application.UnitTests;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Financial;

/// <summary>
/// Integration-level tests for SaleCompletedDomainEventHandler,
/// specifically guarding against BUG#3: TransactionCategory constructor
/// not assigning Id = Guid.NewGuid(), which caused duplicate Guid.Empty PK
/// on re-entrant outbox batches (PostgreSQL 23505).
/// </summary>
public class SaleCompletedDomainEventHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TestApplicationDbContext(options);

        // Seed DealerSettings — required FK for most entities
        context.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(TestDealerId, "Test", "test@test.com"));

        // Seed brand/model
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);

        context.SaveChanges();

        return context;
    }

    private (Car Car, Client Client, User Agent) SeedEntities(TestApplicationDbContext context)
    {
        var marca = context.Marca.First();
        var modelo = context.Modelo.First();

        var car = new Car(
            TestDealerId, marca, modelo,
            Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2022, "TOY001", "desc", 20000m, DateTime.UtcNow);
        context.Cars.Add(car);

        var client = new Client(
            TestDealerId, "John", "Doe", "DNI001", "john@test.com", "555", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);

        var agent = new User(TestDealerId, "agent@test.com", "Agent", "One", "hash", UserRole.Admin);
        context.Users.Add(agent);

        context.SaveChanges();
        return (car, client, agent);
    }

    private static Sale CreateCompletedSale(
        TestApplicationDbContext context,
        Car car,
        Client client,
        User agent,
        string contractNumber)
    {
        var sale = new Sale(
            TestDealerId,
            car.Id,
            client.Id,
            20000m,
            PaymentMethod.BankTransfer,
            contractNumber,
            "Test note",
            DateTime.UtcNow);

        sale.ClearDomainEvents();
        context.Sales.Add(sale);
        context.SaveChanges();

        // Complete the sale — no-arg Complete() uses existing FinalPrice/PaymentMethod
        sale.Complete();
        sale.ClearDomainEvents();
        context.SaveChanges();

        return sale;
    }

    /// <summary>
    /// Two consecutive sale-completed events.
    /// Pre-fix: TransactionCategory constructor leaves Id = Guid.Empty.
    /// Post-fix: first event creates category with non-empty Id;
    /// second event reuses the cached category; both incomes recorded.
    /// </summary>
    [Fact]
    public async Task Handle_TwoSaleCompletedEvents_CreatesCategoryOnce_WithNonEmptyId_AndRecordsBothIncomes()
    {
        // Arrange
        var context = CreateContext();
        var (car, client, agent) = SeedEntities(context);

        var sale1 = CreateCompletedSale(context, car, client, agent, "CTR-001");
        var sale2 = CreateCompletedSale(context, car, client, agent, "CTR-002");

        var dateTimeProvider = new FakeDateTimeProvider();
        var cachedCategoryService = new Mock<ICachedCategoryService>();

        // Simulate cold cache: first call returns null, second call returns the created category
        TransactionCategory? capturedCategory = null;
        cachedCategoryService
            .Setup(s => s.GetByNameAsync("Venta de Auto", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedCategory);

        cachedCategoryService
            .Setup(s => s.InvalidateCacheAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SaleCompletedDomainEventHandler(
            context,
            dateTimeProvider,
            cachedCategoryService.Object);

        // Act — first event
        var event1 = new SaleCompletedDomainEvent(
            sale1.Id, car.Id, client.Id, sale1.FinalPrice, PaymentMethod.BankTransfer);
        await handler.Handle(event1, CancellationToken.None);

        // Capture the created category so the second call finds it
        var createdCategory = await context.TransactionCategories
            .FirstOrDefaultAsync(tc => tc.Name == "Venta de Auto");
        capturedCategory = createdCategory;

        // Act — second event (cache now returns the category)
        var event2 = new SaleCompletedDomainEvent(
            sale2.Id, car.Id, client.Id, sale2.FinalPrice, PaymentMethod.BankTransfer);
        await handler.Handle(event2, CancellationToken.None);

        // Assert
        // Exactly ONE TransactionCategory row
        var categories = await context.TransactionCategories
            .Where(tc => tc.Name == "Venta de Auto")
            .ToListAsync();
        categories.Should().HaveCount(1, "only one category should be created");

        // Id is NOT empty
        categories[0].Id.Should().NotBe(Guid.Empty, "BUG#3: TransactionCategory ctor must set Id = Guid.NewGuid()");

        // Exactly TWO FinancialTransaction rows with that category
        var transactions = await context.Transactions
            .Where(t => t.CategoryId == categories[0].Id)
            .ToListAsync();
        transactions.Should().HaveCount(2, "both completed sales should record an income transaction");
        transactions.All(t => t.Amount.Amount == 20000m).Should().BeTrue();
    }

    /// <summary>
    /// Sanity check: single event creates the category with non-empty Id.
    /// </summary>
    [Fact]
    public async Task Handle_SingleEvent_CreatesCategoryWithNonEmptyId()
    {
        // Arrange
        var context = CreateContext();
        var (car, client, agent) = SeedEntities(context);

        var sale = CreateCompletedSale(context, car, client, agent, "CTR-003");

        var dateTimeProvider = new FakeDateTimeProvider();
        var cachedCategoryService = new Mock<ICachedCategoryService>();
        cachedCategoryService
            .Setup(s => s.GetByNameAsync("Venta de Auto", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionCategory?)null);
        cachedCategoryService
            .Setup(s => s.InvalidateCacheAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SaleCompletedDomainEventHandler(
            context,
            dateTimeProvider,
            cachedCategoryService.Object);

        // Act
        var domainEvent = new SaleCompletedDomainEvent(
            sale.Id, car.Id, client.Id, sale.FinalPrice, PaymentMethod.BankTransfer);
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        var category = await context.TransactionCategories
            .FirstOrDefaultAsync(tc => tc.Name == "Venta de Auto");
        category.Should().NotBeNull();
        category!.Id.Should().NotBe(Guid.Empty, "BUG#3: TransactionCategory ctor must set Id = Guid.NewGuid()");

        var transactions = await context.Transactions
            .Where(t => t.CategoryId == category.Id)
            .ToListAsync();
        transactions.Should().HaveCount(1);
    }
}
