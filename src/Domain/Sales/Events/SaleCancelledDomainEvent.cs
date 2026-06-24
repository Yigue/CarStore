using SharedKernel;

namespace Domain.Sales.Events;

public sealed record SaleCancelledDomainEvent(
    Guid SaleId,
    Guid CarId,
    string Reason) : IDomainEvent;
