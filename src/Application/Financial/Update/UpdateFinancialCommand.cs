using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales;

namespace Application.Financial.Update;

public sealed record UpdateFinancialCommand(
    Guid Id,
    TransactionType Type,
    decimal Amount,
    string Description,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    DateTime TransactionDate,
    Guid CategoryId,
    Guid? CarId,
    Guid? ClientId,
    Guid? SaleId) : ICommand<Guid>;
