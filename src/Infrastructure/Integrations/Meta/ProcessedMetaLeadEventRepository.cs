using Application.Abstractions.Data;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Integrations.Meta;

/// <summary>
/// sfc C1.4 / B13: idempotency on <c>leadgen_id</c>, mirroring <c>Infrastructure.Billing.ProcessedStripeEventRepository</c>'s
/// check-db-then-check-tracker-then-add shape for Stripe events.
/// </summary>
public sealed class ProcessedMetaLeadEventRepository
{
    private readonly IApplicationDbContext _context;

    public ProcessedMetaLeadEventRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryAddAsync(string leadgenId, Guid? dealerId, DateTime processedOnUtc, CancellationToken ct = default)
    {
        bool existsInDb = await _context.ProcessedMetaLeadEvents
            .AnyAsync(e => e.LeadgenId == leadgenId, ct);

        if (existsInDb)
        {
            return false;
        }

        bool existsInTracker = _context.ProcessedMetaLeadEvents.Local
            .Any(e => e.LeadgenId == leadgenId);

        if (existsInTracker)
        {
            return false;
        }

        _context.ProcessedMetaLeadEvents.Add(new ProcessedMetaLeadEvent
        {
            LeadgenId = leadgenId,
            ProcessedOnUtc = processedOnUtc,
            DealerId = dealerId,
        });

        return true;
    }

    /// <summary>Stamps the Lead that the dedup row ended up producing, for traceability only.</summary>
    public void LinkLead(string leadgenId, Guid leadId)
    {
        ProcessedMetaLeadEvent? tracked = _context.ProcessedMetaLeadEvents.Local
            .FirstOrDefault(e => e.LeadgenId == leadgenId);

        if (tracked is not null)
        {
            tracked.LeadId = leadId;
        }
    }
}
