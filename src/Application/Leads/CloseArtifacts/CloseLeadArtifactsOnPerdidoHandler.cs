using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Leads;
using Domain.Leads.Events;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Leads.CloseArtifacts;

/// <summary>
/// LEAD-04: losing a lead closes what was hanging off it.
///
/// <para>
/// A lead marked Perdido used to leave its paperwork open. The worst case was not untidiness: an
/// ACCEPTED quote holds its vehicle Reservado, so a deal that no longer existed kept a car off the
/// floor and nothing in the system ever noticed. Pending sales had the same shape — a draft
/// invoice for a buyer who walked away.
/// </para>
///
/// <para>
/// Nothing is deleted. A rejected quote and a cancelled sale are records of what happened, and
/// they carry the reason, so the funnel report can still tell a lost negotiation from one that was
/// never quoted.
/// </para>
///
/// <para>
/// A lead with a COMPLETED sale is left entirely alone. Money changed hands and a vehicle was
/// delivered; whatever the CRM now says about the enquiry, that is not something a pipeline drag
/// may undo. Such a lead reaching Perdido is a data-entry mistake, and this handler says so in the
/// log instead of unwinding a real sale.
/// </para>
///
/// <para>
/// Runs off the outbox like its siblings, and is idempotent: <see cref="Quote.CloseAsLost"/> is a
/// no-op on an already-closed quote and only Pending sales are cancelled, so a retried message
/// finds nothing left to do.
/// </para>
/// </summary>
internal sealed class CloseLeadArtifactsOnPerdidoHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ILogger<CloseLeadArtifactsOnPerdidoHandler> logger)
    : INotificationHandler<LeadStatusChangedDomainEvent>
{
    internal const string ClosureReason = "Cerrada automáticamente: el lead se marcó como Perdido.";

    public async Task Handle(LeadStatusChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        if (notification.NewStatus != LeadStatus.Perdido)
        {
            return;
        }

        Lead? lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == notification.LeadId, cancellationToken);

        if (lead is null)
        {
            return;
        }

        // The paperwork can hang off either half of the party: quotes and sales raised before the
        // conversion carry the lead, the ones raised after carry the client it became.
        Guid leadId = lead.Id;
        Guid? clientId = lead.ConvertedClientId;

        List<Sale> sales = await context.Sales
            .Where(s => s.LeadId == leadId || (clientId != null && s.ClientId == clientId))
            .ToListAsync(cancellationToken);

        if (sales.Any(s => s.Status == SaleStatus.Completed))
        {
            logger.LogWarning(
                "Lead {LeadId} reached Perdido but has a completed sale. Nothing was closed: a delivered vehicle is not undone by a pipeline change.",
                leadId);
            return;
        }

        List<Quote> quotes = await context.Quotes
            .Where(q => (q.Status == QuoteStatus.Pending || q.Status == QuoteStatus.Accepted)
                && (q.LeadId == leadId || (clientId != null && q.ClientId == clientId)))
            .ToListAsync(cancellationToken);

        List<Sale> openSales = sales.Where(s => s.Status == SaleStatus.Pending).ToList();

        if (quotes.Count == 0 && openSales.Count == 0)
        {
            return;
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;

        foreach (Quote quote in quotes)
        {
            quote.CloseAsLost(ClosureReason, nowUtc);
        }

        foreach (Sale sale in openSales)
        {
            sale.Cancel(ClosureReason);
        }

        await ReleaseHeldCarsAsync(
            quotes.Select(q => q.CarId).Concat(openSales.Select(s => s.CarId)),
            quotes.Select(q => q.Id).ToHashSet(),
            openSales.Select(s => s.Id).ToHashSet(),
            nowUtc,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Lead {LeadId} marked Perdido: closed {QuoteCount} quote(s) and cancelled {SaleCount} sale(s).",
            leadId,
            quotes.Count,
            openSales.Count);
    }

    /// <summary>
    /// Puts back on the floor every unit this lead was holding, and only those.
    /// </summary>
    /// <remarks>
    /// The "is anything else still holding it?" check excludes the rows this handler just closed
    /// BY ID rather than by status. Their new status lives in the change tracker and has not been
    /// written yet, so a status-only query would still see the quote as Accepted and conclude the
    /// car is held — by the very deal being cancelled.
    /// </remarks>
    private async Task ReleaseHeldCarsAsync(
        IEnumerable<Guid> carIds,
        HashSet<Guid> closedQuoteIds,
        HashSet<Guid> cancelledSaleIds,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        foreach (Guid carId in carIds.Distinct())
        {
            Car? car = await context.Cars
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken);

            // Only a reservation is given back. A sold unit is not reopened, and one that was
            // never held has nothing to release.
            if (car is null || car.ServiceCar != StatusServiceCar.Reservado)
            {
                continue;
            }

            bool stillHeld =
                await context.Sales.AnyAsync(
                    s => s.CarId == carId
                        && s.Status != SaleStatus.Cancelled
                        && !cancelledSaleIds.Contains(s.Id),
                    cancellationToken)
                || await context.Quotes.AnyAsync(
                    q => q.CarId == carId
                        && q.Status == QuoteStatus.Accepted
                        && !closedQuoteIds.Contains(q.Id),
                    cancellationToken);

            if (stillHeld)
            {
                continue;
            }

            car.MarkAsAvailable(nowUtc);
        }
    }
}
