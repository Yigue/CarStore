using Application.Abstractions.Tenancy;
using Application.Sales.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Sales;

/// <summary>
/// sfc B7.2 / RN-037: a sale over a vehicle with an Active lien cannot be completed (409 ACTIVE_LIEN_BLOCKS_TRANSFER);
/// once the lien is liquidated it can. A Pending sale can still be recorded.
/// </summary>
public class ActiveLienBlocksSaleTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static CreateSaleCommandHandler Handler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return new CreateSaleCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now }, tenant.Object);
    }

    private static async Task<(TestApplicationDbContext Context, Car Car, Client Client, VehicleLien Lien)> SeedAsync()
    {
        var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var marca = new Marca("Ford");
        var modelo = new Modelo("Ka", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 1400, 50000, 2018, "LIE001", "desc", 9000m, Now.AddDays(-30));
        var client = new Client(DealerId, "Ana", "Perez", "30111222", "ana@x.com", "1", "Addr", Now.AddDays(-30));
        var lien = VehicleLien.Register(DealerId, car.Id, "Banco Norte", Money.Of(3000m, CurrencyCode.Ars), "P-1", Now.AddDays(-20)).Value;
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        context.VehicleLiens.Add(lien);
        await context.SaveChangesAsync();
        return (context, car, client, lien);
    }

    private static CreateSaleCommand Sale(Guid carId, Guid clientId, SaleStatus status) =>
        new(carId, clientId, 9000m, PaymentMethod.Cash, "CN-L", "c", Status: status);

    [Fact]
    public async Task Completing_a_sale_over_an_active_lien_is_refused()
    {
        var (context, car, client, _) = await SeedAsync();
        using var _ = context;

        var result = await Handler(context).Handle(Sale(car.Id, client.Id, SaleStatus.Completed), CancellationToken.None);

        result.Error.Code.Should().Be("ACTIVE_LIEN_BLOCKS_TRANSFER");
        (await context.Sales.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_pending_sale_can_still_be_recorded()
    {
        var (context, car, client, _) = await SeedAsync();
        using var _ = context;

        var result = await Handler(context).Handle(Sale(car.Id, client.Id, SaleStatus.Pending), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Liquidating_the_lien_allows_the_sale()
    {
        var (context, car, client, lien) = await SeedAsync();
        using var _ = context;
        lien.Liquidate(Now.AddDays(-1));
        await context.SaveChangesAsync();

        var result = await Handler(context).Handle(Sale(car.Id, client.Id, SaleStatus.Completed), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
