using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Sales.Attributes;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Sales.Events;

internal sealed class SaleCancelledDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ILogger<SaleCancelledDomainEventHandler> logger
) : INotificationHandler<SaleCancelledDomainEvent>
{
    public async Task Handle(SaleCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling sale cancellation for Sale {SaleId}. Releasing vehicle.", notification.SaleId);

        // A cancelled sale only frees the unit if nothing else is holding it. Releasing
        // unconditionally put a car that another sale had already completed back on the public
        // catalogue — the same column the catalogue filters on (CAT-01) — because an unrelated
        // draft beside it was cancelled.
        bool stillHeld = await context.Sales
            .IgnoreQueryFilters() // the outbox dispatches with no tenant context
            .AnyAsync(
                s => s.CarId == notification.CarId
                    && s.Id != notification.SaleId
                    && (s.Status == SaleStatus.Pending || s.Status == SaleStatus.Completed),
                cancellationToken);

        if (stillHeld)
        {
            logger.LogInformation(
                "Car {CarId} stays held after cancelling Sale {SaleId}: another pending or completed sale still references it.",
                notification.CarId,
                notification.SaleId);
            return;
        }

        Car? car = await context.Cars
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == notification.CarId, cancellationToken);

        if (car is null)
        {
            logger.LogWarning("Car {CarId} not found while handling cancellation for Sale {SaleId}.", notification.CarId, notification.SaleId);
            return;
        }

        // Release the car
        car.MarkAsAvailable(dateTimeProvider.UtcNow);
        
        // Save changes.
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Car {CarId} has been marked as available due to cancellation of Sale {SaleId}.", car.Id, notification.SaleId);
    }
}
