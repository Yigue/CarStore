using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using Domain.Sales.Attributes;

namespace Application.Sales.Update;

public sealed record UpdateSaleCommand(
    Guid Id,
    decimal FinalPrice,
    PaymentMethod PaymentMethod,
    SaleStatus Status,
    // Optional: when omitted/null, the existing value on the sale is kept.
    string? ContractNumber = null,
    string? Comments = null,
    // Optional: when omitted/null, the sale's current salesperson is kept.
    Guid? SalespersonId = null) : ICommand<Guid>;
