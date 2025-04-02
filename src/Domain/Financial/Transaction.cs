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
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public DateTime TransactionDate { get; private set; }

    // Claves externas
    public Guid CategoryId { get; private set; }
    public Guid? CarId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? SaleId { get; private set; }

    // Propiedades de navegación - Cambiamos a init para EF Core
    public TransactionCategory Category { get; private init; }
    public Car? Car { get; private init; }
    public Client? Client { get; private init; }
    public Sale? Sale { get; private init; }

    // Constructor privado para EF Core - IMPORTANTE
    private FinancialTransaction() { }

    // Constructor con todos los parámetros
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

    // Métodos para actualizar propiedades
    public void Update(
        TransactionType type,
        decimal amount,
        string description,
        PaymentMethod paymentMethod,
        string? referenceNumber,
        DateTime transactionDate,
        Guid categoryId,
        Guid? carId,
        Guid? clientId,
        Guid? saleId)
    {
        Type = type;
        Amount = amount;
        Description = description;
        PaymentMethod = paymentMethod;
        ReferenceNumber = referenceNumber;
        TransactionDate = transactionDate;
        CategoryId = categoryId;
        CarId = carId;
        ClientId = clientId;
        SaleId = saleId;
    }
}