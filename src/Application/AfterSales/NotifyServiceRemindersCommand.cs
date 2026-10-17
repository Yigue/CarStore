using System.Globalization;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using Domain.AfterSales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.AfterSales;

/// <summary>sfc C2.4.2 / RN-047: emails pending service reminders to clients who consent. Returns how many were sent.</summary>
public sealed record NotifyServiceRemindersCommand : ICommand<int>;

/// <summary>
/// Runs with no tenant (Quartz), so every query is keyed per reminder/dealer. A reminder is emailed once
/// (<see cref="ServiceReminder.NotifiedAtUtc"/>), only while the client's CURRENT consent is on and the email is usable:
/// a client who opts in later still gets a reminder that is still due. "Due" matches generation: a periodic reminder
/// exists only once its date passed; a warranty reminder exists inside the last 30 days, so it is skipped once the
/// warranty ended (DueOnUtc is the end date). A send failure is logged and leaves the reminder pending for the next run.
/// </summary>
internal sealed class NotifyServiceRemindersCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ITransactionalEmailSender emailSender,
    ILogger<NotifyServiceRemindersCommandHandler> logger)
    : ICommandHandler<NotifyServiceRemindersCommand, int>
{
    public async Task<Result<int>> Handle(NotifyServiceRemindersCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;

        List<ServiceReminder> pending = await context.ServiceReminders
            .Where(r => r.NotifiedAtUtc == null
                        && (r.Kind == ServiceReminderKind.PeriodicService
                            || (r.Kind == ServiceReminderKind.WarrantyExpiring && r.DueOnUtc > now)))
            .ToListAsync(cancellationToken);

        int sent = 0;
        foreach (ServiceReminder reminder in pending)
        {
            var client = await context.Clients
                .Where(c => c.Id == reminder.ClientId && c.DealerId == reminder.DealerId)
                .Select(c => new { c.FirstName, c.LastName, c.AcceptsServiceReminders, Email = c.Email.Value })
                .FirstOrDefaultAsync(cancellationToken);
                if (client is null || !client.AcceptsServiceReminders || string.IsNullOrWhiteSpace(client.Email))
                continue;

            string vehicle = await context.Sales
                .Where(s => s.Id == reminder.SaleId && s.DealerId == reminder.DealerId)
                .Join(context.Cars, s => s.CarId, c => c.Id, (s, c) => c.Marca.Nombre + " " + c.Modelo.Nombre)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
            string dealerName = await context.DealerSettings
                .Where(d => d.DealerId == reminder.DealerId)
                .Select(d => d.DealerName)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var model = new Dictionary<string, string>
            {
                ["Name"] = $"{client.FirstName} {client.LastName}".Trim(),
                ["DealerName"] = dealerName,
                ["VehicleName"] = vehicle,
                ["Message"] = reminder.Kind == ServiceReminderKind.WarrantyExpiring
                    ? "tu garantía está por vencer"
                    : "es momento de realizar el service periódico",
                ["DueOn"] = reminder.DueOnUtc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            };

            try
            {
                await emailSender.SendAsync(EmailTemplate.ServiceReminder, client.Email, model, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                        logger.LogWarning(ex, "Service reminder {ReminderId} could not be emailed", reminder.Id);
                continue;
            }

            if (reminder.MarkNotified(now))
                sent++;
        }

        if (sent > 0)
            await context.SaveChangesAsync(cancellationToken);
        return Result.Success(sent);
    }
}
