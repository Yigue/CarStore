using SharedKernel;
using Domain.Cars;
using Domain.Clients;
using Domain.Sales.Attributes;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales.Events;

namespace Domain.Sales;

public sealed class Sale : Entity
{
    public Guid CarId { get;  set; }
    public Car Car { get;  set; }
    public Guid ClientId { get;  set; }
    public Client Client { get;  set; }
    public decimal FinalPrice { get;  set; }
    public SaleStatus Status { get;  set; }
    public PaymentMethod PaymentMethod { get;  set; }
    public string ContractNumber { get;  set; }
    public DateTime SaleDate { get;  set; }
    public string Comments { get;  set; }
    public List<FinancialTransaction> Transactions { get;  set; }

    // Private parameterless constructor for EF Core
    private Sale()
    {
        Transactions = new List<FinancialTransaction>();
    }

    public Sale(
        Guid carId,
        Guid clientId,
        decimal finalPrice,
        PaymentMethod paymentMethod,
        string contractNumber,
        string comments)
    {
        CarId = carId;
        ClientId = clientId;
        FinalPrice = finalPrice;
        PaymentMethod = paymentMethod;
        ContractNumber = contractNumber;
        Comments = comments;
        Status = SaleStatus.Pending;
        SaleDate = DateTime.UtcNow;
        Transactions = new List<FinancialTransaction>();
        
        // Raise(new SaleCreatedDomainEvent(Id, CarId, ClientId, FinalPrice));
    }


}
