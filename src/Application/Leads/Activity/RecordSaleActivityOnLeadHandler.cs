using Application.Abstractions.Data;
using Domain.Leads;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.Activity;

/// <summary>
/// Closes the loop: the sale that a lead ended in appears in that lead's own history, so "what
/// came of this?" is answerable without leaving the record.
/// </summary>
internal sealed class RecordSaleActivityOnLeadHandler(
    IApplicationDbContext context,
    LeadActivityRecorder recorder,
    IDateTimeProvider dateTimeProvider)
    : INotificationHandler<SaleCreatedDomainEvent>
{
    public async Task Handle(SaleCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        Lead? lead = await ResolveLeadAsync(notification, cancellationToken);

        if (lead is null)
        {
            return;
        }

        bool recorded = await recorder.RecordAsync(
            lead,
            LeadActivityType.SaleRegistered,
            $"Venta registrada por {notification.FinalPrice.Amount:N0} {notification.FinalPrice.Currency}",
            dateTimeProvider.UtcNow,
            cancellationToken,
            relatedEntityId: notification.SaleId,
            relatedEntityType: "Sale");

        if (recorded)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Lead?> ResolveLeadAsync(
        SaleCreatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        if (notification.LeadId is { } leadId)
        {
            return await context.Leads.FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken);
        }

        // A sale closed straight against a client still belongs to the lead that produced them.
        Guid? originLeadId = await context.Clients
            .Where(c => c.Id == notification.ClientId)
            .Select(c => c.OriginLeadId)
            .FirstOrDefaultAsync(cancellationToken);

        return originLeadId is { } origin
            ? await context.Leads.FirstOrDefaultAsync(l => l.Id == origin, cancellationToken)
            : null;
    }
}
