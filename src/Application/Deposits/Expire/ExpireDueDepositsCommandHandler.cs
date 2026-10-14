using System.Globalization;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Deposits;
using Domain.Quotes.Attributes;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Deposits.Expire;

/// <summary>
/// sfc B1.3.3. Runs with no tenant context, so the tenant query filters do not apply and every dealer is swept; each
/// query below works per deposit or per car, never mixing tenants. No money moves on expiry (RN-003).
/// </summary>
internal sealed class ExpireDueDepositsCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ITransactionalEmailSender emailSender,
    ILogger<ExpireDueDepositsCommandHandler> logger)
    : ICommandHandler<ExpireDueDepositsCommand, DepositExpiryRunSummary>
{
    /// <summary>The buyer is warned once the deposit is inside its last 24 hours.</summary>
    public static readonly TimeSpan NoticeWindow = TimeSpan.FromHours(24);

    public async Task<Result<DepositExpiryRunSummary>> Handle(ExpireDueDepositsCommand command, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;

        int noticesSent = await SendExpiringNoticesAsync(now, cancellationToken);

        List<Deposit> due = await context.Deposits
            .Where(d => d.Status == DepositStatus.Active && d.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        int expired = 0;
        int released = 0;
        foreach (Deposit deposit in due)
        {
            if (deposit.Expire(now).IsFailure)
                continue;
            expired++;

            Car? car = await context.Cars.FirstOrDefaultAsync(c => c.Id == deposit.CarId, cancellationToken);
            if (car is null || car.ServiceCar != StatusServiceCar.Reservado)
                continue;

            // H6: the deposit was not necessarily the only thing holding the car.
            bool heldByQuote = await context.Quotes.AnyAsync(
                q => q.CarId == car.Id && q.Status == QuoteStatus.Accepted, cancellationToken);
            bool heldBySale = await context.Sales.AnyAsync(
                s => s.CarId == car.Id && s.Status == SaleStatus.Pending, cancellationToken);
            if (heldByQuote || heldBySale)
                continue;

            car.Release(now);
            released++;
        }

        await context.SaveChangesAsync(cancellationToken);

        if (expired > 0 || noticesSent > 0)
            logger.LogInformation("Deposit sweep: {Expired} expired, {Released} cars released, {Notices} notices sent",
                expired, released, noticesSent);

        return Result.Success(new DepositExpiryRunSummary(expired, released, noticesSent));
    }

    private async Task<int> SendExpiringNoticesAsync(DateTime now, CancellationToken cancellationToken)
    {
        DateTime windowEnd = now.Add(NoticeWindow);
        List<Deposit> expiringSoon = await context.Deposits
            .Where(d => d.Status == DepositStatus.Active
                        && d.ExpiryNoticeSentAtUtc == null
                        && d.ExpiresAtUtc > now
                        && d.ExpiresAtUtc <= windowEnd)
            .ToListAsync(cancellationToken);

        int sent = 0;
        foreach (Deposit deposit in expiringSoon)
        {
            (string? recipient, string name) = await BuyerOfAsync(deposit, cancellationToken);
            if (recipient is null)
                continue;

            Car? car = await context.Cars
                .Include(c => c.Marca)
                .Include(c => c.Modelo)
                .FirstOrDefaultAsync(c => c.Id == deposit.CarId, cancellationToken);
            string dealerName = await context.DealerSettings
                .Where(d => d.DealerId == deposit.DealerId)
                .Select(d => d.DealerName)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var model = new Dictionary<string, string>
            {
                ["Name"] = name,
                ["VehicleName"] = car is null ? string.Empty : $"{car.Marca.Nombre} {car.Modelo.Nombre}",
                ["DealerName"] = dealerName,
                ["ExpiresAt"] = deposit.ExpiresAtUtc.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture),
            };

            try
            {
                await emailSender.SendAsync(EmailTemplate.DepositExpiring, recipient, model, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not marked: the next hourly run retries while the deposit is still inside the window.
                logger.LogWarning(ex, "Expiring notice for deposit {DepositId} could not be sent", deposit.Id);
                continue;
            }

            if (deposit.MarkExpiryNoticeSent(now))
                sent++;
        }

        return sent;
    }

    private async Task<(string? Email, string Name)> BuyerOfAsync(Deposit deposit, CancellationToken cancellationToken)
    {
        if (deposit.ClientId is { } clientId)
        {
            var client = await context.Clients
                .Where(c => c.Id == clientId)
                .Select(c => new { c.FirstName, c.LastName, Email = c.Email.Value })
                .FirstOrDefaultAsync(cancellationToken);
            if (client is not null)
                return (client.Email, $"{client.FirstName} {client.LastName}".Trim());
        }

        if (deposit.LeadId is { } leadId)
        {
            var lead = await context.Leads
                .Where(l => l.Id == leadId)
                .Select(l => new { l.ClientName, Email = l.Email.Value })
                .FirstOrDefaultAsync(cancellationToken);
            if (lead is not null)
                return (lead.Email, lead.ClientName);
        }

        return (null, string.Empty);
    }
}
