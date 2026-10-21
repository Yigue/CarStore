using Domain.Sales.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Sales.Events;

/// <summary>
/// sfc B1.2 / ADR C3: a voided sale puts its car back on sale via <c>Car.RevertSale</c>, applied by <c>VoidSaleCommandHandler</c> in the same
/// transaction (this handler no longer touches the car, so the revert is never applied twice). It stays as the hook for
/// future listeners.
/// <para>
/// sfc B4.1: commission annulment is a SEPARATE handler, <c>VoidSaleCommissionOnSaleVoidedHandler</c> (own
/// <c>SaveChanges</c>) — the placeholder that used to be here is closed. The audit entry (sfc B2.2.4) is not written
/// here either: <c>VoidSaleCommandHandler</c> sets the reason on <c>IAuditContext</c> and <c>TenantAuditInterceptor</c>
/// writes the <c>Sale.Status</c> entry in the same transaction.
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
