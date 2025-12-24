using SharedKernel;
using Domain.Cars;
using Domain.Clients;
using Domain.Sales.Attributes;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;

namespace Domain.Sales;

public sealed class Sale : Entity
{
    public Guid CarId { get; private set; }
    public Car Car { get; private set; }
    public Guid ClientId { get; private set; }
    public Client Client { get; private set; }
    public Money FinalPrice { get; private set; }
    public SaleStatus Status { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string ContractNumber { get; private set; }
    public DateTime SaleDate { get; private set; }
    public string Comments { get; private set; }

    // Private parameterless constructor for EF Core
    private Sale()
    {
    
    }

    public Sale(
        Guid carId,
        Guid clientId,
        decimal finalPrice,
        PaymentMethod paymentMethod,
        string contractNumber,
        string comments)
    {
        Id = Guid.NewGuid();
        CarId = carId;
        ClientId = clientId;
        FinalPrice = new Money(finalPrice);
        PaymentMethod = paymentMethod;
        ContractNumber = contractNumber;
        Comments = comments;
        Status = SaleStatus.Pending;
        SaleDate = DateTime.UtcNow;

        Raise(new SaleCreatedDomainEvent(Id, CarId, ClientId, FinalPrice));
    }

    public void Complete()
    {
        if (Status != SaleStatus.Pending)
            throw new DomainException("Only pending sales can be completed");
        
        Status = SaleStatus.Completed;
        Raise(new SaleCompletedDomainEvent(Id, CarId, ClientId, FinalPrice, PaymentMethod));
    }

    public void Cancel(string reason)
    {
        if (Status != SaleStatus.Pending)
            throw new DomainException("Only pending sales can be cancelled");
        
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Cancellation reason is required");
        
        Status = SaleStatus.Cancelled;
        Raise(new SaleCancelledDomainEvent(Id, reason));
    }
    
    public void Update(
        decimal finalPrice,
        PaymentMethod paymentMethod,
        string contractNumber,
        string comments)
    {
        if (Status != SaleStatus.Pending)
            throw new DomainException("Only pending sales can be updated");
        
        FinalPrice = new Money(finalPrice);
        PaymentMethod = paymentMethod;
        ContractNumber = contractNumber;
        Comments = comments;
    }
    
    public void Update(
        Money finalPrice,
        PaymentMethod paymentMethod,
        string contractNumber,
        string comments)
    {
        if (Status != SaleStatus.Pending)
            throw new DomainException("Only pending sales can be updated");
        
        FinalPrice = finalPrice;
        PaymentMethod = paymentMethod;
        ContractNumber = contractNumber;
        Comments = comments;
    }
}
