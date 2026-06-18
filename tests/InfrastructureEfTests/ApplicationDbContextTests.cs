using System.Data;
using System.Data.Common;
using Application.Abstractions.Tenancy;
using Domain.Appointments;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Sales;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Xunit;

public class ApplicationDbContextTests
{
    private static readonly Guid TestDealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class NoOpTenantService : ICurrentTenantService
    {
        public Guid DealerId => TestDealerId;
        public bool HasTenant => false;
    }

    private static async Task<(ApplicationDbContext Context, SqliteConnection Connection)> CreateContextAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var tenantService = new NoOpTenantService();
        var context = new ApplicationDbContext(options, new NoOpPublisher(), tenantService);
        await context.Database.EnsureCreatedAsync();

        // Seed DealerSettings to avoid FK violations since most entities have a DealerId
        var settings = new Domain.DealerSettings.DealerSettings(TestDealerId, "Test Dealer", "test@dealer.com");
        context.DealerSettings.Add(settings);
        await context.SaveChangesAsync();

        return (context, connection);
    }

    [Fact]
    public async Task CarConfiguration_EnforcesUniquePatente()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var marca = new Marca("Ford");
        var modelo = new Modelo("Fiesta", marca.Id);
        context.AddRange(marca, modelo);
        await context.SaveChangesAsync();

        var car1 = new Car(TestDealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "AAA111", "desc", 10000m, DateTime.UtcNow);
        context.Cars.Add(car1);
        await context.SaveChangesAsync();

        var car2 = new Car(TestDealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "AAA111", "desc", 10000m, DateTime.UtcNow);
        context.Cars.Add(car2);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ClientConfiguration_EnforcesUniqueDni()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var client1 = new Client(TestDealerId, "John", "Doe", "123", "john@test.com", "555", "Street", DateTime.UtcNow);
        var client2 = new Client(TestDealerId, "Jane", "Smith", "123", "jane@test.com", "555", "Street", DateTime.UtcNow);

        context.Clients.Add(client1);
        await context.SaveChangesAsync();

        context.Clients.Add(client2);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SaleConfiguration_RequiresExistingCarAndClient()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var marca = new Marca("Ford");
        var modelo = new Modelo("Fiesta", marca.Id);
        var car = new Car(TestDealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "BBB222", "desc", 9000m, DateTime.UtcNow);
        var client = new Client(TestDealerId, "John", "Doe", "456", "john@demo.com", "555", "Street", DateTime.UtcNow);
        context.AddRange(marca, modelo, car, client);
        await context.SaveChangesAsync();

        var sale = new Sale(TestDealerId, car.Id, client.Id, 9000m, PaymentMethod.Cash, "C-1", "ok", DateTime.UtcNow);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var badSale = new Sale(TestDealerId, Guid.NewGuid(), client.Id, 8000m, PaymentMethod.Cash, "C-2", "bad", DateTime.UtcNow);
        context.Sales.Add(badSale);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task QuoteConfiguration_RequiresExistingCarAndClient()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var marca = new Marca("Ford");
        var modelo = new Modelo("Fiesta", marca.Id);
        var car = new Car(TestDealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "CCC333", "desc", 8000m, DateTime.UtcNow);
        var client = new Client(TestDealerId, "John", "Doe", "789", "john@quotes.com", "555", "Street", DateTime.UtcNow);
        context.AddRange(marca, modelo, car, client);
        await context.SaveChangesAsync();

        var quote = new Quote(TestDealerId, car, client, null, 8000m, Domain.Quotes.Attributes.PaymentMethod.Contado, DateTime.UtcNow.AddDays(30), "ok", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        // Para forzar el error de FK en Quote (que usa objetos en el ctor),
        // creamos un objeto stub y le decimos a EF que ya existe (Unchanged).
        var fakeCar = new Car(TestDealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "ZZZ999", "fake", 1m, DateTime.UtcNow);
        context.Entry(fakeCar).State = EntityState.Unchanged;
        
        var badQuote = new Quote(TestDealerId, fakeCar, client, null, 7000m, Domain.Quotes.Attributes.PaymentMethod.Contado, DateTime.UtcNow.AddDays(30), "bad", DateTime.UtcNow);
        context.Quotes.Add(badQuote);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TransactionConfiguration_SavesWithRequiredCategory()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var category = new TransactionCategory("Venta", "desc", TransactionType.Income);
        context.Add(category);
        await context.SaveChangesAsync();

        var transaction = new FinancialTransaction(TestDealerId, TransactionType.Income, 100m, "desc", PaymentMethod.Cash, category);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();

        transaction.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Migrations_CreateExpectedSchema()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        async Task<List<string>> GetTablesAsync()
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            using var reader = await cmd.ExecuteReaderAsync();
            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
            return tables;
        }

        var tableNames = await GetTablesAsync();

        tableNames.Should().Contain(t => t.EndsWith("cars", StringComparison.OrdinalIgnoreCase));
        tableNames.Should().Contain(t => t.EndsWith("clients", StringComparison.OrdinalIgnoreCase));
        tableNames.Should().Contain(t => t.EndsWith("sales", StringComparison.OrdinalIgnoreCase));
        tableNames.Should().Contain(t => t.EndsWith("quotes", StringComparison.OrdinalIgnoreCase));
        tableNames.Should().Contain(t => t.EndsWith("transactions", StringComparison.OrdinalIgnoreCase));

        async Task<List<string>> GetColumnsAsync(string table)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info('{table}');";
            using var reader = await cmd.ExecuteReaderAsync();
            var cols = new List<string>();
            while (await reader.ReadAsync())
            {
                cols.Add(reader.GetString(1));
            }
            return cols;
        }

        var carsCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("cars", StringComparison.OrdinalIgnoreCase)));
        carsCols.Should().Contain(c => c.Equals("id", StringComparison.OrdinalIgnoreCase));
        carsCols.Should().Contain(c => c.Equals("patente", StringComparison.OrdinalIgnoreCase));

        var clientCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("clients", StringComparison.OrdinalIgnoreCase)));
        clientCols.Should().Contain(c => c.Equals("id", StringComparison.OrdinalIgnoreCase));
        clientCols.Should().Contain(c => c.Equals("dni", StringComparison.OrdinalIgnoreCase));

        var salesCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("sales", StringComparison.OrdinalIgnoreCase)));
        salesCols.Should().Contain(c => c.Equals("id", StringComparison.OrdinalIgnoreCase));
        salesCols.Should().Contain(c => c.Equals("carid", StringComparison.OrdinalIgnoreCase) || c.Equals("car_id", StringComparison.OrdinalIgnoreCase));
        salesCols.Should().Contain(c => c.Equals("clientid", StringComparison.OrdinalIgnoreCase) || c.Equals("client_id", StringComparison.OrdinalIgnoreCase));

        var quoteCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("quotes", StringComparison.OrdinalIgnoreCase)));
        quoteCols.Should().Contain(c => c.Equals("id", StringComparison.OrdinalIgnoreCase));
        quoteCols.Should().Contain(c => c.Equals("carid", StringComparison.OrdinalIgnoreCase) || c.Equals("car_id", StringComparison.OrdinalIgnoreCase));
        quoteCols.Should().Contain(c => c.Equals("clientid", StringComparison.OrdinalIgnoreCase) || c.Equals("client_id", StringComparison.OrdinalIgnoreCase));

        var transactionCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("transactions", StringComparison.OrdinalIgnoreCase)));
        transactionCols.Should().Contain(c => c.Equals("id", StringComparison.OrdinalIgnoreCase));
        transactionCols.Should().Contain(c => c.Equals("categoryid", StringComparison.OrdinalIgnoreCase) || c.Equals("category_id", StringComparison.OrdinalIgnoreCase));
        transactionCols.Should().Contain(c => c.Equals("amount", StringComparison.OrdinalIgnoreCase));
    }
}
