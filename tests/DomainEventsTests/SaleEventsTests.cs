using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Events;

namespace DomainEventsTests;

public class SaleEventsTests
{
    [Fact]
    public void New_sale_raises_SaleCreatedDomainEvent()
    {
        var carId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        const decimal price = 5000m;

        var sale = new Sale(
            carId,
            clientId,
            price,
            PaymentMethod.Cash,
            "C123",
            "none");

        sale.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SaleCreatedDomainEvent>()
            .Which.Should().BeEquivalentTo(new SaleCreatedDomainEvent(sale.Id, carId, clientId, price));
    }
}
