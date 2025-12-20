using SharedKernel;
using Domain.Financial.Attributes;

namespace Domain.Sales.Events;

public sealed record SaleCompletedDomainEvent(
    Guid SaleId,
    Guid CarId,
    Guid ClientId,
    decimal FinalPrice,
    PaymentMethod PaymentMethod) : IDomainEvent;
