using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using Domain.Sales.Attributes;

namespace Application.Sales.Update;

public sealed record UpdateSaleCommand(
    Guid Id,
    decimal FinalPrice,
    PaymentMethod PaymentMethod,
    SaleStatus Status,
    string ContractNumber,
    string Comments) : ICommand<Guid>;
