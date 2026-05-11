using Application.Abstractions.Messaging;

namespace Application.Sales.Delete;

public sealed record DeleteSaleCommand(Guid Id) : ICommand<Guid>;