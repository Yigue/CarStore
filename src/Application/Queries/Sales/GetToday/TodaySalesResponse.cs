using SharedKernel;

namespace Application.Queries.Sales.GetToday;

public sealed record TodaySalesResponse(
    IEnumerable<TodaySaleItem> Sales,
    decimal TotalAmount,
    int Count);

public sealed record TodaySaleItem(
    Guid Id,
    Guid CarId,
    Guid ClientId,
    decimal FinalPrice,
    string PaymentMethod,
    string Status,
    string ContractNumber,
    DateTime SaleDate,
    string Comments,
    string? CarPatente,
    string? ClientName);