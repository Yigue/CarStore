using Domain.Sales.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Sales.Events;

/// <summary>
/// sfc B1.2 / ADR C3: a voided sale puts its car back on sale via <c>Car.RevertSale</c>, applied by <c>VoidSaleCommandHandler</c> in the same
/// transaction (this handler no longer touches the car, so the revert is never applied twice). It stays as the hook for
/// future listeners.
/// <para>
/// PLACEHOLDERS (explicit, not silent): (1) commission annulment is NOT done here, the commission entity belongs
/// to B4 and does not exist yet; (2) the audit entry is NOT written here, the dealer audit log belongs to B2.2
/// (B2.2.3/B2.2.4) and does not exist yet. Until then the actor and reason are only in the logs.
/// </para>
/// </summary>
internal sealed class SaleVoidedDomainEventHandler(
    ILogger<SaleVoidedDomainEventHandler> logger) : INotificationHandler<SaleVoidedDomainEvent>
{
    public Task Handle(SaleVoidedDomainEvent notification, CancellationToken cancellationToken)
    {
        // The car revert is NOT done here: VoidSaleCommandHandler applies it in the same unit of work as the void.
        logger.LogInformation("Sale {SaleId} voided by {ActorId}; Car {CarId} was reverted with the void.",
            notification.SaleId, notification.ActorId, notification.CarId);
        return Task.CompletedTask;
    }
}
