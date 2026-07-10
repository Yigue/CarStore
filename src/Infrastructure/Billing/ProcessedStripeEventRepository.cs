using Application.Abstractions.Data;
using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

public sealed class ProcessedStripeEventRepository
{
    private readonly IApplicationDbContext _context;

    public ProcessedStripeEventRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> TryAddAsync(string stripeEventId, Guid? dealerId, CancellationToken ct = default)
    {
        // Check the database first
        var existsInDb = await _context.ProcessedStripeEvents
            .AnyAsync(e => e.StripeEventId == stripeEventId, ct);

        if (existsInDb)
        {
            return false;
        }

        // Also check the change tracker for pending inserts not yet flushed
        var existsInTracker = _context.ProcessedStripeEvents.Local
            .Any(e => e.StripeEventId == stripeEventId);

        if (existsInTracker)
        {
            return false;
        }

        var processedEvent = new ProcessedStripeEvent
        {
            StripeEventId = stripeEventId,
            ProcessedOnUtc = DateTime.UtcNow,
            DealerId = dealerId
        };

        _context.ProcessedStripeEvents.Add(processedEvent);
        return true;
    }
}
