using SharedKernel;

namespace Domain.Sales.Events;

public sealed record SaleCancelledDomainEvent(
    Guid SaleId,
    string Reason) : IDomainEvent;
