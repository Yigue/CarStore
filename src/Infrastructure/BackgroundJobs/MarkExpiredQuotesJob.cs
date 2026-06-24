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

        // D-1: liberar los vehículos reservados por las cotizaciones que expiran.
        var carIds = expiredQuotes.Select(q => q.CarId).Distinct().ToList();
        var carsById = (await context.Cars
                .IgnoreQueryFilters()
                .Where(c => carIds.Contains(c.Id))
                .ToListAsync(jobContext.CancellationToken))
            .ToDictionary(c => c.Id);

        foreach (var quote in expiredQuotes)
        {
            quote.Expire(now);

            if (carsById.TryGetValue(quote.CarId, out var car))
            {
                car.Release(now);
            }
        }

        await context.SaveChangesAsync(jobContext.CancellationToken);
    }
}
