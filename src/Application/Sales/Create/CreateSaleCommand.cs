using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using Domain.Sales.Attributes;

namespace Application.Sales.Create;

public sealed record CreateSaleCommand(
    Guid CarId,
    Guid ClientId,
    decimal FinalPrice,
    PaymentMethod PaymentMethod,
    string ContractNumber,
    string Comments,
    Guid? LeadId = null,
    Guid? QuoteId = null,
    // Requested initial status. Null (or Pending) leaves the sale Pending — it is
    // only completed immediately when the caller explicitly asks for Completed.
    // Cancelled is rejected as an initial status by the validator.
    SaleStatus? Status = null,
    // Optional salesperson (User) who closed the sale.
    Guid? SalespersonId = null
    ) : ICommand<Guid>;

