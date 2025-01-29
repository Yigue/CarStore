using Application.Abstractions.Messaging;
using Application.Sales.Get;

namespace Application.Sales.GetById;

public sealed record GetSaleByIdQuery(Guid Id) : IQuery<SaleResponse>;
