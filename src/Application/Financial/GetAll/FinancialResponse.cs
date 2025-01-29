using System.Transactions;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;

namespace Application.Financial.GetAll;

public sealed record FinancialResponses(
 Guid Id,
 TransactionType Type,
 decimal Amount,
 string Description,
 PaymentMethod PaymentMethod,
 string? ReferenceNumber,
 DateTime TransactionDate,
 TransactionCategory Category,
 Car? Car,
 Client? Client,
 Sale? Sale
);



    
