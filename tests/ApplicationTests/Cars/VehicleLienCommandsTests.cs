using Application.Abstractions.Tenancy;
using Application.Cars.Liens;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Cars;

/// <summary>sfc B7.2 / RN-037: register a lien on one of the dealer's vehicles and liquidate it; other dealers' are 404.</summary>
public class VehicleLienCommandsTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherDealerId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static ICurrentTenantService Tenant(Guid dealerId)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        return tenant.Object;
    }

    private static async Task<(TestApplicationDbContext Context, Car Car)> SeedAsync()
    {
        var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var marca = new Marca("Ford");
        var modelo = new Modelo("Ka", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 1400, 50000, 2018, "LIE002", "desc", 9000m, Now.AddDays(-30));
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return (context, car);
    }

    [Fact]
    public async Task Register_then_liquidate_a_lien_on_my_vehicle()
    {
        var (context, car) = await SeedAsync();
        using var _ = context;
        var clock = new FakeDateTimeProvider { UtcNow = Now };

        var registered = await new RegisterVehicleLienCommandHandler(context, clock, Tenant(DealerId))
            .Handle(new RegisterVehicleLienCommand(car.Id, "Banco Norte", 3000m, "ARS", "P-9"), CancellationToken.None);
        var liquidated = await new LiquidateVehicleLienCommandHandler(context, clock, Tenant(DealerId))
            .Handle(new LiquidateVehicleLienCommand(registered.Value), CancellationToken.None);

        registered.IsSuccess.Should().BeTrue();
        liquidated.IsSuccess.Should().BeTrue();
        var lien = await context.VehicleLiens.SingleAsync();
        lien.Amount.Should().Be(Money.Of(3000m, CurrencyCode.Ars));
        lien.Status.Should().Be(VehicleLienStatus.Liquidated);
    }

    [Fact]
    public async Task Another_dealer_gets_not_found_on_both_operations()
    {
        var (context, car) = await SeedAsync();
        using var _ = context;
        var clock = new FakeDateTimeProvider { UtcNow = Now };
        var mine = await new RegisterVehicleLienCommandHandler(context, clock, Tenant(DealerId))
            .Handle(new RegisterVehicleLienCommand(car.Id, "Banco", 1m, "ARS", null), CancellationToken.None);

        var register = await new RegisterVehicleLienCommandHandler(context, clock, Tenant(OtherDealerId))
            .Handle(new RegisterVehicleLienCommand(car.Id, "Banco", 1m, "ARS", null), CancellationToken.None);
        var liquidate = await new LiquidateVehicleLienCommandHandler(context, clock, Tenant(OtherDealerId))
            .Handle(new LiquidateVehicleLienCommand(mine.Value), CancellationToken.None);

        register.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
        liquidate.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
        (await context.VehicleLiens.SingleAsync()).Status.Should().Be(VehicleLienStatus.Active);
    }
}
