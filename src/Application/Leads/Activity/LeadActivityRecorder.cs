using Application.Abstractions.Data;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace Application.Leads.Activity;

/// <summary>
/// Shared write path for the lead timeline.
///
/// <para>
/// Every handler here runs off the outbox, which retries. Without a guard a redelivered message
/// writes the entry a second time and the lead's history grows duplicates that look like real
/// repeated activity — the worst kind of wrong, because it reads as plausible. Idempotency is
/// keyed on (lead, type, related entity), which is what "the same thing happened" means for this
/// timeline: one quote can only be accepted once, but a lead can legitimately change status many
/// times, so a status entry carries the transition in its description and is deduped by nothing
/// else.
/// </para>
/// </summary>
internal sealed class LeadActivityRecorder(IApplicationDbContext context)
{
    /// <summary>
    /// Appends an entry unless an identical one already exists. Returns false when it skipped.
    /// Does not save — the caller owns the unit of work.
    /// </summary>
    public async Task<bool> RecordAsync(
        Lead lead,
        LeadActivityType type,
        string description,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? actorUserId = null)
    {
        if (relatedEntityId.HasValue)
        {
            bool alreadyRecorded = await context.LeadActivities
                .AnyAsync(
                    a => a.LeadId == lead.Id
                         && a.Type == type
                         && a.RelatedEntityId == relatedEntityId,
                    cancellationToken);

            if (alreadyRecorded)
            {
                return false;
            }
        }

        context.LeadActivities.Add(LeadActivity.Record(
            lead.DealerId,
            lead.Id,
            type,
            description,
            occurredAtUtc,
            relatedEntityId,
            relatedEntityType,
            actorUserId));

        return true;
    }

    /// <summary>Resolves the lead a quote belongs to, directly or through its client's origin.</summary>
    public async Task<Lead?> FindLeadForQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var quote = await context.Quotes
            .Where(q => q.Id == quoteId)
            .Select(q => new { q.LeadId, q.ClientId })
            .FirstOrDefaultAsync(cancellationToken);

        if (quote is null)
        {
            return null;
        }

        if (quote.LeadId is { } leadId)
        {
            return await context.Leads.FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken);
        }

        // A quote raised before the enquiry started creating leads hangs off a client; that client
        // remembers the lead it came from.
        if (quote.ClientId is { } clientId)
        {
            Guid? originLeadId = await context.Clients
                .Where(c => c.Id == clientId)
                .Select(c => c.OriginLeadId)
                .FirstOrDefaultAsync(cancellationToken);

            if (originLeadId is { } origin)
            {
                return await context.Leads.FirstOrDefaultAsync(l => l.Id == origin, cancellationToken);
            }
        }

        return null;
    }
}
