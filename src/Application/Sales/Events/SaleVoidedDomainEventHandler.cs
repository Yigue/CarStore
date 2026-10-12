using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Sales.Events;

/// <summary>
/// sfc B1.2 / ADR C3: a voided sale puts its car back on sale via <c>Car.RevertSale</c>.
/// <para>
/// PLACEHOLDERS (explicit, not silent): (1) commission annulment is NOT done here, the commission entity belongs
/// to B4 and does not exist yet; (2) the audit entry is NOT written here, the dealer audit log belongs to B2.2
/// (B2.2.3/B2.2.4) and does not exist yet. Until then the actor and reason are only in the logs.
/// </para>
/// </summary>
internal sealed class SaleVoidedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ILogger<SaleVoidedDomainEventHandler> logger) : INotificationHandler<SaleVoidedDomainEvent>
{
    public async Task Handle(SaleVoidedDomainEvent notification, CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .IgnoreQueryFilters() // the outbox dispatches with no tenant context
            .FirstOrDefaultAsync(c => c.Id == notification.CarId, cancellationToken);

        if (car is null)
        {
            logger.LogWarning("Car {CarId} not found while handling void of Sale {SaleId}.", notification.CarId, notification.SaleId);
            return;
        }

        Result revert = car.RevertSale(notification.SaleId, dateTimeProvider.UtcNow);
        if (revert.IsFailure)
        {
            logger.LogWarning("Car {CarId} was not reverted for voided Sale {SaleId}: {Error}.",
                notification.CarId, notification.SaleId, revert.Error.Code);
            return;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Placeholder: audit (B2.2) and commission annulment (B4) are pending; see class remarks.
        logger.LogInformation("Sale {SaleId} voided by {ActorId}; Car {CarId} reverted to Disponible.",
            notification.SaleId, notification.ActorId, car.Id);
    }
}
