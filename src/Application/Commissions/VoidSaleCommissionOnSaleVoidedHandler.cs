using Application.Abstractions.Data;
using Domain.Commissions;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Commissions;

/// <summary>
/// sfc B4.1: closes the <c>SaleVoidedDomainEventHandler</c> placeholder from B1.2.2 — a voided sale
/// voids its commission. A sale with no <see cref="SaleCommission"/> row (completed before this
/// feature shipped, or never configured) is a safe no-op, never an error.
/// </summary>
internal sealed class VoidSaleCommissionOnSaleVoidedHandler(
    IApplicationDbContext context,
    ILogger<VoidSaleCommissionOnSaleVoidedHandler> logger)
    : INotificationHandler<SaleVoidedDomainEvent>
{
    public async Task Handle(SaleVoidedDomainEvent notification, CancellationToken cancellationToken)
    {
        SaleCommission? commission = await context.SaleCommissions
            .FirstOrDefaultAsync(c => c.SaleId == notification.SaleId, cancellationToken);

        if (commission is null)
        {
            logger.LogInformation(
                "No SaleCommission found for voided Sale {SaleId}; no-op.", notification.SaleId);
            return;
        }

        commission.Void();
        await context.SaveChangesAsync(cancellationToken);
    }
}
