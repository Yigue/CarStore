using Domain.Financial.Attributes;
using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Sales.Events;

public sealed record SaleCompletedDomainEvent(
    Guid SaleId,
    Guid CarId,
    Guid ClientId,
    Money FinalPrice,
    PaymentMethod PaymentMethod) : IDomainEvent;
