using Application.Abstractions.Data;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;
using SharedKernel;

namespace Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public sealed class MarkExpiredQuotesJob(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IJob
{
    public async Task Execute(IJobExecutionContext jobContext)
    {
        var now = dateTimeProvider.UtcNow;

        var expiredQuotes = await context.Quotes
            .IgnoreQueryFilters() // Job runs globally across all tenants
            .Where(q => q.Status == QuoteStatus.Pending && q.ValidUntil < now)
            .ToListAsync(jobContext.CancellationToken);

        if (expiredQuotes.Count == 0)
        {
            return;
        }

        // Only Pending quotes expire, and a Pending quote holds no reservation — the hold moved
        // to acceptance, so several offers can sit on one car at once. Releasing the car here
        // would hand back a unit that a competing ACCEPTED quote is holding, days after the
        // dealership committed it. Expiring an offer means the offer lapsed, nothing more.
        foreach (var quote in expiredQuotes)
        {
            quote.Expire(now);
        }

        await context.SaveChangesAsync(jobContext.CancellationToken);
    }
}
