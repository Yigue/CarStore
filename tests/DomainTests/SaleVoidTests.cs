using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Sales.Events;

namespace DomainTests;

/// <summary>sfc ADR C3 / H4 — Sale.Void: Completed → Voided, mandatory reason (RN-006).</summary>
public class SaleVoidTests
{
    private static readonly DateTime At = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private static Sale NewSale() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3_000_000m,
        PaymentMethod.Cash, "C-1", "", At);

    private static Sale CompletedSale()
    {
        Sale sale = NewSale();
        sale.Complete();
        sale.ClearDomainEvents();
        return sale;
    }

    [Fact]
    public void RN006_Void_CompletedSale_TransitionsToVoidedAndRaisesEvent()
    {
        Sale sale = CompletedSale();
        Guid actor = Guid.NewGuid();

        Result result = sale.Void("Cliente desistió", actor, At);

        result.IsSuccess.Should().BeTrue();
        sale.Status.Should().Be(SaleStatus.Voided);
        SaleVoidedDomainEvent evt = sale.DomainEvents.OfType<SaleVoidedDomainEvent>().Single();
        evt.SaleId.Should().Be(sale.Id);
        evt.CarId.Should().Be(sale.CarId);
        evt.Reason.Should().Be("Cliente desistió");
        evt.ActorId.Should().Be(actor);
        evt.OccurredAtUtc.Should().Be(At);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RN006_Void_WithoutReason_FailsAndKeepsStatus(string? reason)
    {
        Sale sale = CompletedSale();

        Result result = sale.Void(reason!, Guid.NewGuid(), At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.VoidReasonRequired());
        sale.Status.Should().Be(SaleStatus.Completed);
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RN006_Void_PendingSale_Fails()
    {
        Sale sale = NewSale();

        Result result = sale.Void("x", Guid.NewGuid(), At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.OnlyCompletedCanBeVoided(sale.Id));
        sale.Status.Should().Be(SaleStatus.Pending);
    }

    [Fact]
    public void RN006_Void_AlreadyVoided_Fails()
    {
        Sale sale = CompletedSale();
        sale.Void("x", Guid.NewGuid(), At);

        sale.Void("again", Guid.NewGuid(), At).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Cancel_StillAcceptsOnlyPending()
    {
        Sale sale = CompletedSale();

        Action act = () => sale.Cancel("nope");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_PendingSale_StillWorks()
    {
        Sale sale = NewSale();
        sale.Cancel("motivo");
        sale.Status.Should().Be(SaleStatus.Cancelled);
    }

    [Fact]
    public void RN006_VoidedSaleEvent_LetsCarReturnToDisponible()
    {
        // The handler (wiring, pending) reacts to SaleVoidedDomainEvent with Car.RevertSale.
        var marca = new Domain.Cars.Attributes.Marca("Toyota");
        var modelo = new Domain.Cars.Attributes.Modelo("Corolla", marca.Id) { Marca = marca };
        var car = new Domain.Cars.Car(Guid.NewGuid(), marca, modelo, Domain.Cars.Attributes.Color.Silver,
            Domain.Cars.Attributes.TypeCar.Sedan, Domain.Cars.Attributes.StatusCar.Certified,
            Domain.Cars.Attributes.StatusServiceCar.Disponible, 4, 5, 1800, 1, 2023, "ABC123", "d", 1m, At);
        car.MarkAsSold(At);
        Sale sale = CompletedSale();
        sale.Void("x", Guid.NewGuid(), At);
        SaleVoidedDomainEvent evt = sale.DomainEvents.OfType<SaleVoidedDomainEvent>().Single();

        car.RevertSale(evt.SaleId, evt.OccurredAtUtc).IsSuccess.Should().BeTrue();

        car.ServiceCar.Should().Be(Domain.Cars.Attributes.StatusServiceCar.Disponible);
    }
}
