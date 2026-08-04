using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Sales.Events;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Xunit;

namespace WebApiTests.Postgres;

[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class SaleCompletedCarStatusHandlerPostgresTests : IAsyncLifetime
{
    private const string AdminDealerId = "11111111-1111-1111-1111-111111111111";

    private readonly PostgresWebApplicationFactory _factory;

    public SaleCompletedCarStatusHandlerPostgresTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [Fact]
    public async Task SaleCompletedDomainEvent_Published_UpdatesCarStatusToVendido()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var dealerId = Guid.Parse(AdminDealerId);

        var marca = new Marca("Ford " + Guid.NewGuid());
        var modelo = new Modelo("Focus " + Guid.NewGuid(), marca.Id);
        db.Marca.Add(marca);
        db.Modelo.Add(modelo);

        var car = new Car(
            dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 2000, 1000, 2023,
            "PAT" + Guid.NewGuid().ToString("N")[..5], "desc", 15000m, DateTime.UtcNow);
        db.Cars.Add(car);

        await db.SaveChangesAsync();

        var handler = new SaleCompletedCarStatusHandler(db, new FakeDateTimeProvider());

        var domainEvent = new SaleCompletedDomainEvent(
            Guid.NewGuid(), car.Id, Guid.NewGuid(), new Money(15000m), Domain.Financial.Attributes.PaymentMethod.Cash);

        await handler.Handle(domainEvent, CancellationToken.None);

        var updatedCar = await db.Cars.FindAsync(car.Id);
        updatedCar.Should().NotBeNull();
        updatedCar!.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }
}
