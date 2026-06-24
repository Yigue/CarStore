using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Sales.Events;
using MediatR;
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

        Car? car = await context.Cars.FindAsync([notification.CarId], cancellationToken);
        
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
