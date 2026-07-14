using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using SharedKernel;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Daily sweep that sends a one-shot re-engagement email to leads that have sat in
/// <see cref="LeadStatus.Perdido"/> for a while and haven't been re-engaged yet.
/// Disabled by default (<c>Crm:Reengagement:Enabled = false</c>) — no email goes out
/// until an operator explicitly opts in.
/// <para>
/// KNOWN SIMPLIFICATION: "older than N days" is measured from <c>Lead.CreatedAt</c>,
/// not from the moment the lead actually transitioned to Perdido — the Lead aggregate
/// has no dedicated "lost at" timestamp (only <c>LeadStatusChangedDomainEvent</c> rows
/// in the outbox capture that transition, and joining against outbox history was judged
/// out of scope for v1). In practice CRM lead lifecycles here are short, so the drift
/// between "days since creation" and "days since lost" is expected to be small.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class LeadReengagementJob(
    IApplicationDbContext context,
    IEmailService emailService,
    IOptions<CrmReengagementOptions> options,
    IDateTimeProvider dateTimeProvider,
    ILogger<LeadReengagementJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext jobContext)
    {
        CrmReengagementOptions settings = options.Value;

        if (!settings.Enabled)
        {
            return;
        }

        DateTime now = dateTimeProvider.UtcNow;
        DateTime cutoff = now.AddDays(-settings.DaysAfterLost);

        List<Lead> candidates = await context.Leads
            .IgnoreQueryFilters() // job runs globally across all tenants
            .Where(l =>
                l.Status == LeadStatus.Perdido &&
                l.ReengagementSentAtUtc == null &&
                l.CreatedAt <= cutoff)
            .ToListAsync(jobContext.CancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        Dictionary<Guid, string> dealerNamesById = (await context.DealerSettings
                .IgnoreQueryFilters()
                .Where(d => candidates.Select(l => l.DealerId).Distinct().Contains(d.DealerId))
                .Select(d => new { d.DealerId, d.DealerName })
                .ToListAsync(jobContext.CancellationToken))
            .ToDictionary(d => d.DealerId, d => d.DealerName);

        foreach (Lead lead in candidates)
        {
            // Defensive guard: Lead.Email is a mandatory value object today (constructor
            // throws on empty), so this branch is currently unreachable. Kept in case that
            // invariant ever relaxes.
            if (string.IsNullOrWhiteSpace(lead.Email?.Value))
            {
                continue;
            }

            string dealerName = dealerNamesById.GetValueOrDefault(lead.DealerId, "CarStore");

            try
            {
                await emailService.SendEmailAsync(
                    lead.Email.Value,
                    $"{dealerName} — ¿seguís buscando tu próximo auto?",
                    BuildBody(lead, dealerName),
                    jobContext.CancellationToken);

                // Only stamped after the mail server accepted the message — a transient
                // SMTP failure must leave the lead eligible again on the next run, not
                // silently burn its one-shot re-engagement (v1 semantics).
                lead.MarkReengagementSent(now);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to send re-engagement email for Lead {LeadId}. Will retry on next run.",
                    lead.Id);
            }
        }

        await context.SaveChangesAsync(jobContext.CancellationToken);
    }

    private static string BuildBody(Lead lead, string dealerName) => $"""
        <html>
        <body>
          <p>Hola {lead.ClientName},</p>
          <p>Notamos que tu búsqueda con {dealerName} quedó pausada. Si segu&iacute;s buscando tu próximo auto, nos encantaría ayudarte de nuevo.</p>
          <p>Respondé este correo o visitanos cuando quieras — el equipo de {dealerName} está para ayudarte.</p>
        </body>
        </html>
        """;
}
