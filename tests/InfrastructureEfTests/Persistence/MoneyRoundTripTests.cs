using Application.Abstractions.Tenancy;
using Domain.Cars;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Quotes;
using Domain.Sales;
using Domain.Shared.ValueObjects;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureEfTests.Persistence;

/// <summary>
/// ADR C1 / task B0.1 (saas-functional-completeness): the currency of a monetary amount is
/// persisted next to the amount. Before the fix <c>MoneyValueConverter</c> kept only the decimal
/// and rebuilt every <see cref="Money"/> as "ARS", so a USD price silently became ARS.
/// Runs on relational SQLite so the real column mapping is exercised, not the in-memory provider.
/// </summary>
public class MoneyRoundTripTests
{
    private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private sealed class FakeTenant : ICurrentTenantService
    {
        public Guid DealerId => TenantId;
        public bool HasTenant => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private static ApplicationDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new ApplicationDbContext(options, new NoOpPublisher(), new FakeTenant());
    }

    private static async Task<SqliteConnection> OpenSchemaAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var ctx = NewContext(connection);
        await ctx.Database.EnsureCreatedAsync();
        ctx.DealerSettings.Add(new DealerSettingsEntity(TenantId, "Test Dealer", "test@dealer.com"));
        await ctx.SaveChangesAsync();
        return connection;
    }

    private static Car NewCar(Marca marca, Modelo modelo, string plate) => new(
        TenantId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.Used,
        StatusServiceCar.Disponible, 4, 5, 1600, 90_000, 2020, plate, "car",
        10_000m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), FuelType.Gasolina, false, Transmission.Manual, null);

    private static void SetPrivate(object target, string property, object? value) =>
        target.GetType().GetProperty(property)!.SetValue(target, value);

    // RN: la moneda de un monto no se pierde al guardar y releer (ADR C1, B0.1.1).
    [Fact]
    public async Task CarPrice_InUsd_Should_StayUsdAfterRoundTrip()
    {
        await using var connection = await OpenSchemaAsync();
        Guid carId;
        await using (var write = NewContext(connection))
        {
            var marca = new Marca("Toyota");
            var modelo = new Modelo("Corolla", marca.Id);
            write.Marca.Add(marca);
            write.Modelo.Add(modelo);
            var car = NewCar(marca, modelo, "USD001");
            car.UpdatePrice(new Money(100m, "USD"), DateTime.UtcNow);
            write.Cars.Add(car);
            await write.SaveChangesAsync();
            carId = car.Id;
        }

        await using var read = NewContext(connection);
        var loaded = await read.Cars.AsNoTracking().SingleAsync(c => c.Id == carId);

        loaded.Price.Should().Be(new Money(100m, "USD"));
    }

    [Fact]
    public async Task CarPrice_InArs_Should_StayArsAfterRoundTrip()
    {
        await using var connection = await OpenSchemaAsync();
        Guid carId;
        await using (var write = NewContext(connection))
        {
            var marca = new Marca("Toyota");
            var modelo = new Modelo("Corolla", marca.Id);
            write.Marca.Add(marca);
            write.Modelo.Add(modelo);
            var car = NewCar(marca, modelo, "ARS001");
            car.UpdatePrice(new Money(2500.5m, "ARS"), DateTime.UtcNow);
            write.Cars.Add(car);
            await write.SaveChangesAsync();
            carId = car.Id;
        }

        await using var read = NewContext(connection);
        (await read.Cars.AsNoTracking().SingleAsync(c => c.Id == carId)).Price
            .Should().Be(new Money(2500.5m, "ARS"));
    }

    [Fact]
    public async Task QuoteProposedPrice_InUsd_Should_StayUsdAfterRoundTrip()
    {
        await using var connection = await OpenSchemaAsync();
        Guid quoteId;
        await using (var write = NewContext(connection))
        {
            var marca = new Marca("Toyota");
            var modelo = new Modelo("Corolla", marca.Id);
            write.Marca.Add(marca);
            write.Modelo.Add(modelo);
            var car = NewCar(marca, modelo, "QUO001");
            var client = new Client(TenantId, "Ana", "Perez", "30111222", "ana@x.com", "1155550000", "Calle 1", DateTime.UtcNow);
            write.Cars.Add(car);
            write.Clients.Add(client);
            var now = DateTime.UtcNow;
            var quote = new Quote(TenantId, car, client, null, 1m, Domain.Quotes.Attributes.PaymentMethod.Contado, now.AddDays(5), "c", now);
            quote.Update(new Money(900m, "USD"), now.AddDays(5), "c", now);
            write.Quotes.Add(quote);
            await write.SaveChangesAsync();
            quoteId = quote.Id;
        }

        await using var read = NewContext(connection);
        (await read.Quotes.AsNoTracking().SingleAsync(q => q.Id == quoteId)).ProposedPrice
            .Should().Be(new Money(900m, "USD"));
    }

    [Fact]
    public async Task SaleAmounts_InUsd_Should_StayUsd_AndMissingPartsStayNull()
    {
        await using var connection = await OpenSchemaAsync();
        Guid saleId;
        await using (var write = NewContext(connection))
        {
            var marca = new Marca("Toyota");
            var modelo = new Modelo("Corolla", marca.Id);
            write.Marca.Add(marca);
            write.Modelo.Add(modelo);
            var car = NewCar(marca, modelo, "SAL001");
            var client = new Client(TenantId, "Ana", "Perez", "30111222", "ana@x.com", "1155550000", "Calle 1", DateTime.UtcNow);
            write.Cars.Add(car);
            write.Clients.Add(client);
            var sale = new Sale(TenantId, car.Id, client.Id, 1m, PaymentMethod.Cash, "C-1", "c", DateTime.UtcNow);
            sale.Update(new Money(1000m, "USD"), PaymentMethod.Cash, "C-1", "c");
            SetPrivate(sale, nameof(Sale.DownPayment), new Money(300m, "USD"));
            SetPrivate(sale, nameof(Sale.FinancedAmount), new Money(700m, "USD"));
            write.Sales.Add(sale);
            await write.SaveChangesAsync();
            saleId = sale.Id;
        }

        await using var read = NewContext(connection);
        var loaded = await read.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId);

        loaded.FinalPrice.Should().Be(new Money(1000m, "USD"));
        loaded.DownPayment.Should().Be(new Money(300m, "USD"));
        loaded.FinancedAmount.Should().Be(new Money(700m, "USD"));
        // "no amount recorded" is not "amount of zero" (NullableMoneyValueConverter's reason to exist).
        loaded.TradeInValue.Should().BeNull();
        loaded.InstallmentAmount.Should().BeNull();
    }

    [Fact]
    public async Task TransactionAmount_InUsd_Should_StayUsdAfterRoundTrip()
    {
        await using var connection = await OpenSchemaAsync();
        Guid txId;
        await using (var write = NewContext(connection))
        {
            var category = new TransactionCategory(TenantId, "Ventas", "Ventas", TransactionType.Income);
            write.TransactionCategories.Add(category);
            var tx = new FinancialTransaction(
                TenantId, TransactionType.Income, new Money(250m, "USD"), "desc",
                PaymentMethod.Cash, category);
            write.Transactions.Add(tx);
            await write.SaveChangesAsync();
            txId = tx.Id;
        }

        await using var read = NewContext(connection);
        (await read.Transactions.AsNoTracking().SingleAsync(t => t.Id == txId)).Amount
            .Should().Be(new Money(250m, "USD"));
    }

    // Expand/contract: during a blue/green deploy the previous version keeps inserting rows without
    // the currency columns, so the migration gives them a DB default. A default currency on a row
    // whose amount is NULL must still read back as "no amount", never as a zero-valued Money.
    [Fact]
    public async Task OptionalAmount_WithDefaultCurrencyButNoAmount_Should_ReadAsNull()
    {
        await using var connection = await OpenSchemaAsync();
        Guid saleId;
        await using (var write = NewContext(connection))
        {
            var marca = new Marca("Toyota");
            var modelo = new Modelo("Corolla", marca.Id);
            write.Marca.Add(marca);
            write.Modelo.Add(modelo);
            var car = NewCar(marca, modelo, "SAL002");
            var client = new Client(TenantId, "Ana", "Perez", "30111223", "ana2@x.com", "1155550001", "Calle 1", DateTime.UtcNow);
            write.Cars.Add(car);
            write.Clients.Add(client);
            var sale = new Sale(TenantId, car.Id, client.Id, 500m, PaymentMethod.Cash, "C-2", "c", DateTime.UtcNow);
            write.Sales.Add(sale);
            await write.SaveChangesAsync();
            saleId = sale.Id;
        }

        await using (var raw = connection.CreateCommand())
        {
            raw.CommandText = "UPDATE sales SET trade_in_value_currency = 'ARS', down_payment_currency = 'ARS'";
            await raw.ExecuteNonQueryAsync();
        }

        await using var read = NewContext(connection);
        var loaded = await read.Sales.AsNoTracking().SingleAsync(s => s.Id == saleId);
        loaded.TradeInValue.Should().BeNull();
        loaded.DownPayment.Should().BeNull();
    }
}
