using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;

namespace Application.Sales.Create;

public sealed record CreateSaleCommand(
    Guid CarId,
    Guid ClientId,
    decimal FinalPrice,
    PaymentMethod PaymentMethod,
    string Status,
    string ContractNumber,
    string Comments
    ) : ICommand<Guid>;

