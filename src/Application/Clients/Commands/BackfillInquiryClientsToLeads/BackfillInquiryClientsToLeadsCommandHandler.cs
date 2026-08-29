using System.Diagnostics;
using System.Text.Json;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Clients;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.Commands.BackfillInquiryClientsToLeads;

/// <summary>
/// Rebuilds the Leads that public web enquiries should have produced before
/// <c>CreateInquiryCommandHandler</c> was fixed. Until then every enquiry created a Client and
/// anchored its Quote to that Client, so those prospects never appeared in the CRM pipeline and
/// never will on their own.
///
/// <para><b>Identifying them.</b> The old handler built
/// <c>new Client(dealerId, first, last, "", email, phone, "", now)</c>, leaving an empty DNI, an
/// empty address and no origin lead. That fingerprint is discriminating: the other anonymous
/// path that creates Clients — the newsletter — writes <c>DNI = "NL-…"</c> and
/// <c>Address = "Suscripto via Web"</c> (see <c>Newsletter/Subscribe.cs</c>), so subscribers are
/// never caught by this.</para>
///
/// <para><b>Who is spared.</b> A client with a sale or an accepted quote is a real customer
/// whatever route created them; demoting one back to a prospect would corrupt the commercial
/// record. Those are excluded outright.</para>
///
/// Idempotent: converted clients are soft-deleted and their quotes re-pointed, so a second run
/// finds nothing. Appends one audit row per invocation, DryRun included.
/// </summary>
internal sealed class BackfillInquiryClientsToLeadsCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<BackfillInquiryClientsToLeadsCommand, BackfillInquiryClientsToLeadsResult>
{
    public async Task<Result<BackfillInquiryClientsToLeadsResult>> Handle(
        BackfillInquiryClientsToLeadsCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.DryRun && !command.Confirmed)
        {
            return Result.Failure<BackfillInquiryClientsToLeadsResult>(new Error(
                "Backfill.NotConfirmed",
                "Apply requires Confirmed=true. Use DryRun=true to preview.",
                ErrorType.Validation));
        }

        Guid dealerId = tenantService.DealerId;
        if (dealerId == Guid.Empty)
        {
            return Result.Failure<BackfillInquiryClientsToLeadsResult>(new Error(
                "Backfill.NoTenant",
                "Cannot run backfill without a resolved tenant.",
                ErrorType.Validation));
        }

        var stopwatch = Stopwatch.StartNew();
        BackfillAction action = command.DryRun ? BackfillAction.DryRun : BackfillAction.Apply;

        List<Client> candidates = await context.Clients
            .Where(c => c.DealerId == dealerId
                        // Explicit, not left to the global filter. This operation retires rows and
                        // rewrites quote ownership, so its selection must not depend on an ambient
                        // predicate that a future refactor or an IgnoreQueryFilters() could remove
                        // — that is what makes a second run a no-op instead of a duplicate pass.
                        && !c.IsDeleted
                        && c.DNI == string.Empty
                        && c.Address == string.Empty
                        && c.OriginLeadId == null
                        // A sale or an accepted quote makes this a real customer, not a prospect.
                        && !context.Sales.Any(s => s.ClientId == c.Id)
                        && !context.Quotes.Any(q => q.ClientId == c.Id && q.Status == QuoteStatus.Accepted))
            .ToListAsync(cancellationToken);

        List<Guid> convertedClientIds = candidates.Select(c => c.Id).ToList();
        int reassignedQuoteCount = 0;

        if (!command.DryRun && candidates.Count > 0)
        {
            reassignedQuoteCount = await ConvertAsync(candidates, dealerId, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        else if (command.DryRun && candidates.Count > 0)
        {
            reassignedQuoteCount = await context.Quotes
                .CountAsync(q => q.ClientId != null && convertedClientIds.Contains(q.ClientId.Value), cancellationToken);
        }

        stopwatch.Stop();

        BackfillAudit audit = BackfillAudit.Create(
            dealerId,
            userContext.UserId,
            action,
            candidates.Count,
            (int)stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(convertedClientIds),
            dateTimeProvider.UtcNow);

        context.BackfillAudits.Add(audit);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new BackfillInquiryClientsToLeadsResult(
            audit.Id,
            action,
            candidates.Count,
            convertedClientIds,
            reassignedQuoteCount));
    }

    private async Task<int> ConvertAsync(
        List<Client> candidates,
        Guid dealerId,
        CancellationToken cancellationToken)
    {
        List<Guid> candidateIds = candidates.Select(c => c.Id).ToList();

        List<Quote> quotes = await context.Quotes
            .Where(q => q.ClientId != null && candidateIds.Contains(q.ClientId.Value))
            .ToListAsync(cancellationToken);

        int reassigned = 0;

        foreach (Client client in candidates)
        {
            var lead = Lead.Create(
                dealerId,
                $"{client.FirstName} {client.LastName}".Trim(),
                client.Email.Value,
                client.Phone ?? string.Empty,
                LeadSource.Web,
                client.CreatedAt);

            // The vehicle the person actually asked about, taken from the enquiry's own quote.
            Quote? firstQuote = quotes
                .Where(q => q.ClientId == client.Id)
                .OrderBy(q => q.CreatedAt)
                .FirstOrDefault();

            if (firstQuote is not null)
            {
                lead.LinkVehicle(firstQuote.CarId);
            }

            if (!string.IsNullOrWhiteSpace(client.Notes))
            {
                lead.UpdateNotes(client.Notes);
            }

            context.Leads.Add(lead);

            foreach (Quote quote in quotes.Where(q => q.ClientId == client.Id))
            {
                quote.AssignLead(lead.Id);
                reassigned++;
            }

            // Soft delete, not physical: the row keeps whatever else still references it, and an
            // operator can restore it from the existing clients trash if a conversion was wrong.
            client.Delete(userContext.UserId, dateTimeProvider.UtcNow);
        }

        return reassigned;
    }
}
