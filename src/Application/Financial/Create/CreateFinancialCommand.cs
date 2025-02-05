using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;

namespace Application.Financial.Create;

public sealed record CreateFinancialCommand(
    TransactionType Type,
    decimal Amount,
    string Description,
    PaymentMethod PaymentMethod,
    Guid CategoryId,
    Guid? CarId,
    Guid? ClientId,
    Guid? SaleId ) : ICommand<Guid>;
