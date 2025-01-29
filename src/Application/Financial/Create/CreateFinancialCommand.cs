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
    TransactionCategory Category,
    Car? Car ,
    Client Client ,
    Sale? Sale ) : ICommand<Guid>;
