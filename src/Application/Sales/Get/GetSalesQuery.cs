using Application.Abstractions.Messaging;

namespace Application.Sales.Get;

public sealed record GetSalesQuery : IQuery<List<SaleResponse>>;
