using SharedKernel;

namespace Domain.Sales.Events;

public sealed record SaleCompletedDomainEvent(Guid SaleId) : IDomainEvent;
