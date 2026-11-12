using Domain.Cars;
using Domain.Cars.Attributes;

namespace DomainTests.Cars;

/// <summary>
/// sfc B1 / ADR C3 — vehicle lifecycle state machine (RN-005, RN-006, RN-007).
/// Ingreso → Reacondicionamiento → Disponible → Reservado → Vendido → Entregado.
/// </summary>
public class VehicleLifecycleTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private static Car NewCar(StatusServiceCar status)
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id) { Marca = marca };
        return new Car(Guid.NewGuid(), marca, modelo, Color.Silver, TypeCar.Sedan, StatusCar.Certified,
            status, 4, 5, 1800, 15000, 2023, "ABC123", "desc", 22000m, Now);
    }

    // ── RN-005: transition table ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(StatusServiceCar.Ingreso, StatusServiceCar.Reacondicionamiento)]
    [InlineData(StatusServiceCar.Ingreso, StatusServiceCar.Disponible)]
    [InlineData(StatusServiceCar.Reacondicionamiento, StatusServiceCar.Disponible)]
    [InlineData(StatusServiceCar.Disponible, StatusServiceCar.Reservado)]
    [InlineData(StatusServiceCar.Reservado, StatusServiceCar.Disponible)]
    [InlineData(StatusServiceCar.Reservado, StatusServiceCar.Vendido)]
    [InlineData(StatusServiceCar.Disponible, StatusServiceCar.Vendido)]
    [InlineData(StatusServiceCar.Vendido, StatusServiceCar.Entregado)]
    public void RN005_AllowedTransitions_AreAllowed(StatusServiceCar from, StatusServiceCar to)
    {
        VehicleLifecycle.CanTransition(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusServiceCar.Ingreso, StatusServiceCar.Vendido)]
    [InlineData(StatusServiceCar.Ingreso, StatusServiceCar.Reservado)]
    [InlineData(StatusServiceCar.Reacondicionamiento, StatusServiceCar.Reservado)]
    [InlineData(StatusServiceCar.Disponible, StatusServiceCar.Ingreso)]
    [InlineData(StatusServiceCar.Disponible, StatusServiceCar.Entregado)]
    [InlineData(StatusServiceCar.Reservado, StatusServiceCar.Entregado)]
    [InlineData(StatusServiceCar.Entregado, StatusServiceCar.Disponible)]
    [InlineData(StatusServiceCar.Entregado, StatusServiceCar.Vendido)]
    public void RN005_DisallowedTransitions_AreRejected(StatusServiceCar from, StatusServiceCar to)
    {
        VehicleLifecycle.CanTransition(from, to).Should().BeFalse();
    }

    [Theory]
    [InlineData(StatusServiceCar.EnVenta, StatusServiceCar.Disponible)]
    [InlineData(StatusServiceCar.Service, StatusServiceCar.Reacondicionamiento)]
    [InlineData(StatusServiceCar.NoDisponible, StatusServiceCar.Ingreso)]
    public void LegacyStatuses_NormalizeToLifecycleStages(StatusServiceCar legacy, StatusServiceCar stage)
    {
        VehicleLifecycle.Normalize(legacy).Should().Be(stage);
    }

    [Fact]
    public void RN005_LegacyEnVenta_CanBeReserved()
    {
        VehicleLifecycle.CanTransition(StatusServiceCar.EnVenta, StatusServiceCar.Reservado).Should().BeTrue();
    }

    // ── RN-006: Vendido → anything only via sale void ────────────────────────────────────────

    [Theory]
    [InlineData(StatusServiceCar.Disponible)]
    [InlineData(StatusServiceCar.EnVenta)]
    [InlineData(StatusServiceCar.Reservado)]
    [InlineData(StatusServiceCar.Ingreso)]
    [InlineData(StatusServiceCar.Reacondicionamiento)]
    public void RN006_ChangeStatus_FromVendido_IsBlockedWithSaleActiveError(StatusServiceCar target)
    {
        Car car = NewCar(StatusServiceCar.Vendido);

        Result result = car.ChangeStatus(target, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.SaleActiveBlocksStatusChange(car.Id));
        car.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }

    [Fact]
    public void RN006_RevertSale_ReturnsVendidoCarToDisponible()
    {
        Car car = NewCar(StatusServiceCar.Vendido);

        Result result = car.RevertSale(Guid.NewGuid(), Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        car.ServiceCar.Should().Be(StatusServiceCar.Disponible);
        car.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Theory]
    [InlineData(StatusServiceCar.Disponible)]
    [InlineData(StatusServiceCar.Reservado)]
    [InlineData(StatusServiceCar.Entregado)]
    public void RN006_RevertSale_OnlyValidFromVendido(StatusServiceCar status)
    {
        Car car = NewCar(status);

        Result result = car.RevertSale(Guid.NewGuid(), Now);

        result.IsFailure.Should().BeTrue();
        car.ServiceCar.Should().Be(status);
    }

    // ── RN-005/RN-007: guarded domain methods ────────────────────────────────────────────────

    [Fact]
    public void RN005_ChangeStatus_ValidTransition_Applies()
    {
        Car car = NewCar(StatusServiceCar.Ingreso);

        car.ChangeStatus(StatusServiceCar.Reacondicionamiento, Now.AddMinutes(1)).IsSuccess.Should().BeTrue();
        car.ChangeStatus(StatusServiceCar.Disponible, Now.AddMinutes(2)).IsSuccess.Should().BeTrue();

        car.ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }

    [Fact]
    public void RN005_ChangeStatus_InvalidTransition_FailsAndKeepsState()
    {
        Car car = NewCar(StatusServiceCar.Ingreso);

        Result result = car.ChangeStatus(StatusServiceCar.Vendido, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.InvalidStatusTransition(
            car.Id, StatusServiceCar.Ingreso, StatusServiceCar.Vendido));
        car.ServiceCar.Should().Be(StatusServiceCar.Ingreso);
    }

    [Fact]
    public void RN005_ChangeStatus_SameStatus_IsIdempotent()
    {
        Car car = NewCar(StatusServiceCar.Disponible);

        car.ChangeStatus(StatusServiceCar.Disponible, Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RN007_ChangeStatus_ToVendido_RaisesSoldEvent()
    {
        Car car = NewCar(StatusServiceCar.Disponible);
        car.ClearDomainEvents();

        car.ChangeStatus(StatusServiceCar.Vendido, Now).IsSuccess.Should().BeTrue();

        car.ServiceCar.Should().Be(StatusServiceCar.Vendido);
        car.DomainEvents.Should().ContainSingle(e => e is Domain.Cars.Events.CarSoldDomainEvent);
    }

    [Fact]
    public void RN005_MarkDelivered_OnlyFromVendido()
    {
        NewCar(StatusServiceCar.Vendido).MarkDelivered(Now).IsSuccess.Should().BeTrue();
        NewCar(StatusServiceCar.Disponible).MarkDelivered(Now).IsFailure.Should().BeTrue();
    }

    // ── existing callers keep working ────────────────────────────────────────────────────────

    [Fact]
    public void ExistingCallers_ReserveReleaseSell_StillWork()
    {
        Car car = NewCar(StatusServiceCar.Disponible);
        car.Reserve(Now);
        car.Release(Now);
        car.MarkAsSold(Now);
        car.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }
}
