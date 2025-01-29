using SharedKernel;

namespace Domain.Sales.Events;

public sealed record SaleCreatedDomainEvent(
    Guid SaleId,
    Guid CarId,
    Guid ClientId,
    decimal FinalPrice) : IDomainEvent;
