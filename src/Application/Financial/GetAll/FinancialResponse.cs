using Domain.Financial.Attributes;

namespace Application.Financial.GetAll;

public sealed record FinancialResponses(
    Guid Id,
    TransactionType Type,
    decimal Amount,
    string Description,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    DateTime Date,
    Guid? CategoryId,
    string? CategoryName,
    Guid? CarId,
    string? CarLabel,
    Guid? ClientId,
    string? ClientName,
    Guid? SaleId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid TenantId
);
