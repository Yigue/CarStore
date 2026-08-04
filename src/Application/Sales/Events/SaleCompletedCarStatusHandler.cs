using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Events;

internal sealed class SaleCompletedCarStatusHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider
) : INotificationHandler<SaleCompletedDomainEvent>
{
    public async Task Handle(SaleCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == notification.CarId, cancellationToken);

        if (car is null)
        {
            return;
        }

        car.MarkAsSold(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);
    }
}
