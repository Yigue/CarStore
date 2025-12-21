using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Sales.Events;
using Domain.Financial.Attributes;
using SharedKernel;

public class SaleTests
{
    private readonly Faker _faker = new();

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var carId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var finalPrice = _faker.Random.Decimal(1000, 100000);
        var paymentMethod = _faker.PickRandom<PaymentMethod>();
        var contractNumber = _faker.Random.ReplaceNumbers("########");
        var comments = _faker.Lorem.Sentence();

        var sale = new Sale(carId, clientId, finalPrice, paymentMethod, contractNumber, comments);

        sale.CarId.Should().Be(carId);
        sale.ClientId.Should().Be(clientId);
        sale.FinalPrice.Amount.Should().Be(finalPrice);
        sale.PaymentMethod.Should().Be(paymentMethod);
        sale.ContractNumber.Should().Be(contractNumber);
        sale.Comments.Should().Be(comments);
        sale.Status.Should().Be(SaleStatus.Pending);
        sale.SaleDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        sale.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SaleErrors_ShouldReturnExpectedValues()
    {
        var saleId = Guid.NewGuid();

        var alreadySold = SalesErrors.AlreadySold(saleId);
        alreadySold.Code.Should().Be("Sales.AlreadySold");
        alreadySold.Description.Should().Be($"The sale with Id = '{saleId}' is already sold.");
        alreadySold.Type.Should().Be(ErrorType.Problem);

        var notFound = SalesErrors.NotFound(saleId);
        notFound.Code.Should().Be("Sales.NotFound");
        notFound.Description.Should().Be($"The sale with the Id = '{saleId}' was not found");
        notFound.Type.Should().Be(ErrorType.NotFound);

        var quoteExpired = SalesErrors.QuoteExpired(saleId);
        quoteExpired.Code.Should().Be("Sales.QuoteExpired");
        quoteExpired.Description.Should().Be($"The quote with the Id = '{saleId}' was not found");
        quoteExpired.Type.Should().Be(ErrorType.NotFound);

        var notAll = SalesErrors.NotAllAtributes(saleId);
        notAll.Code.Should().Be("Sales.NotAllAttributes");
        notAll.Description.Should().Be($"The sale with the Id = '{saleId}' was not found");
        notAll.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void SaleEvents_ShouldContainCorrectData()
    {
        var saleId = Guid.NewGuid();
        var carId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var finalPrice = _faker.Random.Decimal(1000, 10000);
        var reason = _faker.Lorem.Sentence();

        var created = new SaleCreatedDomainEvent(saleId, carId, clientId, finalPrice);
        created.SaleId.Should().Be(saleId);
        created.CarId.Should().Be(carId);
        created.ClientId.Should().Be(clientId);
        created.FinalPrice.Should().Be(finalPrice);

        var completed = new SaleCompletedDomainEvent(saleId);
        completed.SaleId.Should().Be(saleId);

        var cancelled = new SaleCancelledDomainEvent(saleId, reason);
        cancelled.SaleId.Should().Be(saleId);
        cancelled.Reason.Should().Be(reason);
    }
}
