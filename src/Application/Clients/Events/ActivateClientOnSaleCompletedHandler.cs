using Application.Abstractions.Data;
using Domain.Clients;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Clients.Events;

internal sealed class ActivateClientOnSaleCompletedHandler(IApplicationDbContext context)
    : INotificationHandler<SaleCompletedDomainEvent>
{
    public async Task Handle(SaleCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        Client? client = await context.Clients
            .FirstOrDefaultAsync(c => c.Id == notification.ClientId, cancellationToken);

        if (client is null)
        {
            return;
        }

        client.Activate();

        await context.SaveChangesAsync(cancellationToken);
    }
}
