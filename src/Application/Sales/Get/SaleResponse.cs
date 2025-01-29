namespace Application.Sales.Get;

public sealed class SaleResponse
{
    public Guid Id { get; init; }
    public Guid CarId { get; init; }
    public Guid ClientId { get; init; }

    public decimal FinalPrice { get; init; }
    public string PaymentMethod { get; init; }
    public string Status { get; init; }
    public string ContractNumber { get; init; }
    public DateTime SaleDate { get; init; }
    public string Comments { get; init; }
    public string CarBrand { get; init; }
    public string CarModel { get; init; }
    public string ClientName { get; init; }
    public List<TransactionResponse> Transactions { get; init; }
}

public sealed class TransactionResponse
{
    public Guid Id { get; init; }
    public decimal Amount { get; init; }
    public string Type { get; init; }
    public DateTime Date { get; init; }
    public string Description { get; init; }
}
