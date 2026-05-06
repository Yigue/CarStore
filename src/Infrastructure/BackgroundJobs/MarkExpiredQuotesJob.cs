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

        foreach (var quote in expiredQuotes)
        {
            quote.Expire(now);
        }

        await context.SaveChangesAsync(jobContext.CancellationToken);
    }
}
