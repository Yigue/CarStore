using System;
using System.Threading.Tasks;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Sales;
using Domain.Sales.Attributes;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PaymentMethod = Domain.Financial.Attributes.PaymentMethod;

namespace WebApiTests.Postgres;

[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class SaleInvariantPostgresTests : IAsyncLifetime
{
    private const string AdminDealerId = "11111111-1111-1111-1111-111111111111";

    private readonly PostgresWebApplicationFactory _factory;

    public SaleInvariantPostgresTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Postgres_InsertingTwoCompletedSalesForSameCar_ThrowsUniqueConstraintViolation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var dealerId = Guid.Parse(AdminDealerId);

        var marca = new Marca("Toyota " + Guid.NewGuid());
        var modelo = new Modelo("Corolla " + Guid.NewGuid(), marca.Id);
        db.Marca.Add(marca);
        db.Modelo.Add(modelo);

        var car = new Car(
            dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2022,
            "AA" + Random.Shared.Next(100, 999) + "CC", "desc", 10000m, DateTime.UtcNow);
        db.Cars.Add(car);

        var client = new Client(
            dealerId, "John", "Doe", Guid.NewGuid().ToString("N")[..8],
            $"{Guid.NewGuid():N}@test.com", "111", "Addr", DateTime.UtcNow);
        db.Clients.Add(client);

        await db.SaveChangesAsync();

        var sale1 = new Sale(
            dealerId, car.Id, client.Id, 10000m, PaymentMethod.Cash,
            "CN-001", "first sale", DateTime.UtcNow);
        sale1.Complete();
        db.Sales.Add(sale1);
        await db.SaveChangesAsync();

        var sale2 = new Sale(
            dealerId, car.Id, client.Id, 10000m, PaymentMethod.Cash,
            "CN-002", "second completed sale", DateTime.UtcNow);
        sale2.Complete();
        db.Sales.Add(sale2);

        Func<Task> act = async () => await db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be("23505");
    }
}
