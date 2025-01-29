using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Clients;
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
 TransactionCategory Category,
 Car Car,
 Client Client,
 Sale Sale) : ICommand<Guid>;
