using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.AfterSales;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.AfterSales;

/// <summary>sfc C2.4 / RN-049: the daily after-sales sweep, run by <c>ServiceReminderJob</c> with no tenant. Returns how many reminders it created.</summary>
public sealed record GenerateServiceRemindersCommand : ICommand<int>;

/// <summary>
/// For each completed and delivered sale: the most recent periodic service point already due (every N months after
/// delivery, per dealer, 6 by default) and, while a warranty is active, one reminder in its last 30 days. An ended
/// warranty produces nothing. Existing reminders are never repeated. Client consent is captured on each reminder.
/// </summary>
internal sealed class GenerateServiceRemindersCommandHandler(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    : ICommandHandler<GenerateServiceRemindersCommand, int>
{
    public static readonly TimeSpan WarrantyNoticeWindow = TimeSpan.FromDays(30);

    public async Task<Result<int>> Handle(GenerateServiceRemindersCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;
        Dictionary<Guid, int> cadence = await context.DealerSettings
            .ToDictionaryAsync(d => d.DealerId, d => d.ServiceReminderMonths, cancellationToken);

        var sales = await context.Sales
            .Where(s => s.Status == SaleStatus.Completed && s.DeliveryDate != null && s.DeliveryDate <= now)
            .Select(s => new { s.Id, s.DealerId, s.ClientId, Delivered = s.DeliveryDate!.Value })
            .ToListAsync(cancellationToken);
        var saleIds = sales.Select(s => s.Id).ToList();

        var existing = (await context.ServiceReminders
                .Where(r => saleIds.Contains(r.SaleId))
                .Select(r => new { r.SaleId, r.Kind, r.DueOnUtc })
                .ToListAsync(cancellationToken))
            .Select(r => (r.SaleId, r.Kind, r.DueOnUtc))
            .ToHashSet();

        var clientIds = sales.Select(s => s.ClientId).Distinct().ToList();
        Dictionary<Guid, bool> consent = await context.Clients
            .Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.AcceptsServiceReminders, cancellationToken);

        int created = 0;
        void Add(Guid dealerId, Guid saleId, Guid clientId, ServiceReminderKind kind, DateTime due)
        {
            if (!existing.Add((saleId, kind, due)))
                return;
            context.ServiceReminders.Add(ServiceReminder.Create(
                dealerId, saleId, clientId, kind, due, consent.GetValueOrDefault(clientId), now));
            created++;
        }

        foreach (var sale in sales)
        {
            int months = cadence.GetValueOrDefault(sale.DealerId, Domain.DealerSettings.DealerSettings.DefaultServiceReminderMonths);
            if (months <= 0)
                continue;

            int elapsedPeriods = 0;
            while (sale.Delivered.AddMonths((elapsedPeriods + 1) * months) <= now)
                elapsedPeriods++;
            if (elapsedPeriods > 0)
                Add(sale.DealerId, sale.Id, sale.ClientId, ServiceReminderKind.PeriodicService, sale.Delivered.AddMonths(elapsedPeriods * months));
        }

        DateTime noticeLimit = now.Add(WarrantyNoticeWindow);
        var expiring = await context.WarrantyRecords
            .Where(w => w.StartsOnUtc <= now && w.EndsOnUtc > now && w.EndsOnUtc <= noticeLimit)
            .Select(w => new { w.DealerId, w.SaleId, w.ClientId, w.EndsOnUtc })
            .ToListAsync(cancellationToken);
        if (expiring.Count > 0)
        {
            var expiringSaleIds = expiring.Select(w => w.SaleId).ToList();
            foreach (var r in await context.ServiceReminders.Where(r => expiringSaleIds.Contains(r.SaleId))
                         .Select(r => new { r.SaleId, r.Kind, r.DueOnUtc }).ToListAsync(cancellationToken))
                existing.Add((r.SaleId, r.Kind, r.DueOnUtc));
            foreach (Guid clientId in expiring.Select(w => w.ClientId).Where(id => !consent.ContainsKey(id)).Distinct().ToList())
                consent[clientId] = await context.Clients.Where(c => c.Id == clientId).Select(c => c.AcceptsServiceReminders).FirstOrDefaultAsync(cancellationToken);
        }

        foreach (var warranty in expiring)
            Add(warranty.DealerId, warranty.SaleId, warranty.ClientId, ServiceReminderKind.WarrantyExpiring, warranty.EndsOnUtc);

        if (created > 0)
            await context.SaveChangesAsync(cancellationToken);
        return Result.Success(created);
    }
}
