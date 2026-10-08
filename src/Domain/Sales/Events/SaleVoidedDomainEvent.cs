using SharedKernel;

namespace Domain.Sales.Events;

/// <summary>
/// ADR C3: a Completed sale was voided. The handler (wiring) reverts the car with
/// <c>Car.RevertSale</c>, voids the commission and writes the audit entry.
/// </summary>
public sealed record SaleVoidedDomainEvent(
    Guid SaleId,
    Guid CarId,
    string Reason,
    Guid ActorId,
    DateTime OccurredAtUtc) : IDomainEvent;
