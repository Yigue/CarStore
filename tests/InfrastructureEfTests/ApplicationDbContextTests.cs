using System.Data;
using System.Data.Common;

public class ApplicationDbContextTests
{
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static async Task<(ApplicationDbContext Context, SqliteConnection Connection)> CreateContextAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ApplicationDbContext(options, new NoOpPublisher());
        await context.Database.MigrateAsync();
        return (context, connection);
    }

    [Fact]
    public async Task CarConfiguration_EnforcesUniquePatente()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var marca = new Marca("Ford") { Id = Guid.NewGuid() };
        var modelo = new Modelo("Fiesta", marca.Id) { Id = Guid.NewGuid() };
        context.AddRange(marca, modelo);
        await context.SaveChangesAsync();

        var car1 = new Car(marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, statusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "AAA111", "desc", 10000m, DateTime.UtcNow);
        context.Cars.Add(car1);
        await context.SaveChangesAsync();

        var car2 = new Car(marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, statusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "AAA111", "desc", 10000m, DateTime.UtcNow);
        context.Cars.Add(car2);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ClientConfiguration_EnforcesUniqueDni()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var client1 = new Client("John", "Doe", "123", "john@test.com", "555", "Street") { Id = Guid.NewGuid() };
        var client2 = new Client("Jane", "Smith", "123", "jane@test.com", "555", "Street") { Id = Guid.NewGuid() };

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

        var marca = new Marca("Ford") { Id = Guid.NewGuid() };
        var modelo = new Modelo("Fiesta", marca.Id) { Id = Guid.NewGuid() };
        var car = new Car(marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, statusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "BBB222", "desc", 9000m, DateTime.UtcNow);
        var client = new Client("John", "Doe", "456", "john@demo.com", "555", "Street") { Id = Guid.NewGuid() };
        context.AddRange(marca, modelo, car, client);
        await context.SaveChangesAsync();

        var sale = new Sale(car.Id, client.Id, 9000m, PaymentMethod.Cash, "C-1", "ok");
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var badSale = new Sale(Guid.NewGuid(), client.Id, 8000m, PaymentMethod.Cash, "C-2", "bad");
        context.Sales.Add(badSale);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task QuoteConfiguration_RequiresExistingCarAndClient()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var marca = new Marca("Ford") { Id = Guid.NewGuid() };
        var modelo = new Modelo("Fiesta", marca.Id) { Id = Guid.NewGuid() };
        var car = new Car(marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, statusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "CCC333", "desc", 8000m, DateTime.UtcNow);
        var client = new Client("John", "Doe", "789", "john@quotes.com", "555", "Street") { Id = Guid.NewGuid() };
        context.AddRange(marca, modelo, car, client);
        await context.SaveChangesAsync();

        var quote = new Quote(car, client, 8000m, DateTime.UtcNow.AddDays(30), "ok");
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var badQuote = new Quote(car, client, 7000m, DateTime.UtcNow.AddDays(30), "bad")
        {
            Car = null!,
            CarId = Guid.NewGuid()
        };
        context.Quotes.Add(badQuote);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TransactionConfiguration_SavesWithRequiredCategory()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = context;
        await using var __ = connection;

        var category = new TransactionCategory("Venta", "desc", TransactionType.Income) { Id = Guid.NewGuid() };
        context.Add(category);
        await context.SaveChangesAsync();

        var transaction = new FinancialTransaction(TransactionType.Income, 100m, "desc", PaymentMethod.Cash, category);
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

        var tables = connection.GetSchema("Tables");
        var tableNames = tables.Rows.Cast<DataRow>().Select(r => r["TABLE_NAME"].ToString()!).ToList();

        tableNames.Should().Contain(t => t.EndsWith("cars"));
        tableNames.Should().Contain(t => t.EndsWith("clients"));
        tableNames.Should().Contain(t => t.EndsWith("sales"));
        tableNames.Should().Contain(t => t.EndsWith("quotes"));
        tableNames.Should().Contain(t => t.EndsWith("transactions"));

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

        var carsCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("cars")));
        carsCols.Should().Contain(new[] { "id", "patente" });

        var clientCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("clients")));
        clientCols.Should().Contain(new[] { "id", "dni" });

        var salesCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("sales")));
        salesCols.Should().Contain(new[] { "id", "car_id", "client_id" });

        var quoteCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("quotes")));
        quoteCols.Should().Contain(new[] { "id", "car_id", "client_id" });

        var transactionCols = await GetColumnsAsync(tableNames.First(t => t.EndsWith("transactions")));
        transactionCols.Should().Contain(new[] { "id", "category_id", "amount" });
    }
}
