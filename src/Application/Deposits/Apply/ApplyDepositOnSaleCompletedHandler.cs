using Application.Abstractions.Data;
using Domain.Deposits;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Deposits.Apply;

/// <summary>
/// sfc B1.3 / spec deposit-management: Active → Applied when a sale of the deposit's buyer over the car completes. Runs from the outbox with no
/// tenant; it touches only the deposit of the event's car.
/// </summary>
internal sealed class ApplyDepositOnSaleCompletedHandler(IApplicationDbContext context)
    : INotificationHandler<SaleCompletedDomainEvent>
{
    public async Task Handle(SaleCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        Deposit? deposit = await context.Deposits.FirstOrDefaultAsync(
            d => d.CarId == notification.CarId && d.Status == DepositStatus.Active, cancellationToken);
        if (deposit is null)
            return;

        // Only the deposit's own buyer: a sale to someone else must never consume it.
        Guid? clientOriginLeadId = await context.Clients
            .Where(c => c.Id == notification.ClientId)
            .Select(c => c.OriginLeadId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!deposit.BelongsTo(notification.ClientId, clientOriginLeadId))
            return;

        if (deposit.Apply(notification.SaleId).IsSuccess)
            await context.SaveChangesAsync(cancellationToken);
    }
}
