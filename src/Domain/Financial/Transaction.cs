using SharedKernel;
using Domain.Cars;
using Domain.Clients;
using Domain.Sales;
using Domain.Financial.Attributes;
using Domain.Financial.Events;

namespace Domain.Financial;

public sealed class FinancialTransaction : Entity
{
    // Propiedades principales
    public TransactionType Type { get;  set; }
    public decimal Amount { get;  set; }
    public string Description { get;  set; }
    public PaymentMethod PaymentMethod { get;  set; }
    public string? ReferenceNumber { get;  set; }
    public DateTime TransactionDate { get;  set; }

    // Claves externas
    public Guid CategoryId { get;  set; }
    public Guid? CarId { get;  set; }
    public Guid? ClientId { get;  set; }
    public Guid? SaleId { get;  set; }

    // Propiedades de navegación - Cambiamos a init para EF Core
    public TransactionCategory Category { get;  init; }
    public Car? Car { get;  init; }
    public Client? Client { get;  init; }
    public Sale? Sale { get;  init; }

    // Constructor privado para EF Core - IMPORTANTE
    private FinancialTransaction() { }

    // Constructor público con los campos mínimos requeridos
    public FinancialTransaction(
        TransactionType type,
        decimal amount,
        string description,
        PaymentMethod paymentMethod,
        TransactionCategory category,
        Car? car = null,
        Client? client = null,
        Sale? sale = null)
    {
        Type = type;
        Amount = amount;
        Description = description;
        PaymentMethod = paymentMethod;
        Category = category;
        CategoryId = category.Id;
        Car = car;
        if (car != null)
        {
            CarId = car.Id;
        }
        Client = client;
        if (client != null)
        {
            ClientId = client.Id;
        }
        Sale = sale;
        if (sale != null)
        {
            SaleId = sale.Id;
        }
        TransactionDate = DateTime.UtcNow;
    }


}
