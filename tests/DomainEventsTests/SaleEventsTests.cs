using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;

namespace DomainEventsTests;

public class SaleEventsTests
{
    [Fact]
    public void New_sale_raises_SaleCreatedDomainEvent()
    {
        var carId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        const decimal price = 5000m;
        var money = new Money(price);

        var sale = new Sale(
            carId,
            clientId,
            price,
            PaymentMethod.Cash,
            "C123",
            "none");

        sale.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SaleCreatedDomainEvent>()
            .Which.Should().BeEquivalentTo(new SaleCreatedDomainEvent(sale.Id, carId, clientId, money));
    }
}
