using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Create;

internal sealed class CreateQuoteCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService tenantService)
    : ICommandHandler<CreateQuoteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        // Validate ValidUntil is in the future
        if (command.ValidUntil <= dateTimeProvider.UtcNow)
        {
            return Result.Failure<Guid>(QuoteErrors.InvalidValidUntil());
        }
        
        Car? car = await context.Cars
            .SingleOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));
        }

        // A quote is an offer, not a commitment. Several buyers asking for a price on the same
        // unit is the normal case, and the old rule — quote only a Disponible car, then reserve
        // it — turned the first offer into an exclusive hold: the second buyer got a 409 and the
        // salesperson got a board they could not move.
        //
        // Two things still make a car unquotable, and both mean the unit is genuinely gone:
        if (car.ServiceCar == StatusServiceCar.Vendido)
        {
            return Result.Failure<Guid>(CarErrors.NotAvailable(command.CarId));
        }

        bool alreadyCommitted = await context.Quotes
            .AnyAsync(q => q.CarId == command.CarId && q.Status == QuoteStatus.Accepted, cancellationToken);

        if (alreadyCommitted)
        {
            return Result.Failure<Guid>(QuoteErrors.CarAlreadyCommitted(command.CarId));
        }

        // Resolve exactly one party: an existing client, or a lead (which lets the
        // quote-accepted handlers auto-convert the lead into a client).
        Client? client = null;
        Lead? lead = null;

        if (command.ClientId is { } clientId)
        {
            client = await context.Clients
                .SingleOrDefaultAsync(c => c.Id == clientId, cancellationToken);

            if (client is null)
            {
                return Result.Failure<Guid>(ClientErrors.NotFound(clientId));
            }

            // REQ-QT-GATE-001: a Lost client is a commercially dead deal — reject the
            // quote before reserving the car. Use-case precondition, not an aggregate
            // invariant (design.md §1) — the Quote constructor stays untouched.
            if (client.Status == ClientStatus.Lost)
            {
                return Result.Failure<Guid>(QuoteErrors.ClientNotQuotable(client.Id));
            }
        }
        else if (command.LeadId is { } leadId)
        {
            // IgnoreQueryFilters: bypasses LeadConfiguration's Archivado-hiding filter
            // so the gate below can return the precise LeadNotQuotable error instead
            // of a misleading NotFound for a lead that exists but is archived.
            // IMPORTANT: IgnoreQueryFilters() also strips the tenant (DealerId) query
            // filter defined on Lead, so the DealerId check below is NOT optional —
            // without it, a leadId belonging to another dealer would resolve
            // successfully and get linked into a persisted Quote (cross-tenant leak,
            // see verify-report crm-cotizaciones-etapa3 CRITICAL-1).
            lead = await context.Leads
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    l => l.Id == leadId && l.DealerId == tenantService.DealerId,
                    cancellationToken);

            if (lead is null)
            {
                return Result.Failure<Guid>(LeadErrors.NotFound(leadId));
            }

            // REQ-QT-GATE-001: Perdido/Archivado leads cannot be legitimately quoted.
            if (lead.Status is LeadStatus.Perdido or LeadStatus.Archivado)
            {
                return Result.Failure<Guid>(QuoteErrors.LeadNotQuotable(lead.Id));
            }
        }

        if (client is null && lead is null)
        {
            return Result.Failure<Guid>(new Error(
                "Quotes.MissingParty",
                "A quote must reference either a client or a lead.",
                ErrorType.Validation));
        }

        // Resolve the OTHER half of the same person. A converted lead and the client it became
        // are one party, and which of the two the operator happened to pick in the form should
        // not decide what the quote remembers — or which pipeline rules can still see it.
        if (lead is null && client is { OriginLeadId: { } originLeadId })
        {
            lead = await context.Leads
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    l => l.Id == originLeadId && l.DealerId == tenantService.DealerId,
                    cancellationToken);
        }
        else if (client is null && lead is { ConvertedClientId: { } convertedClientId })
        {
            client = await context.Clients
                .SingleOrDefaultAsync(c => c.Id == convertedClientId, cancellationToken);
        }

        var quote = new Quote(
            tenantService.DealerId,
            car,
            client,
            lead,
            command.ProposedPrice,
            command.PaymentMethod,
            command.ValidUntil,
            command.Comments,
            dateTimeProvider.UtcNow);

        context.Quotes.Add(quote);

        // No reservation here on purpose. The car is committed when a quote is ACCEPTED
        // (AcceptQuoteCommandHandler), which is the one moment that has to be exclusive.

        // Advance the lead HERE, in this transaction. AdvanceLeadOnQuoteCreatedHandler does the
        // same thing off the outbox, but that is a Quartz job on a ten-second tick: the board
        // refetched right after the quote was saved, saw the lead unmoved, and the next drag
        // opened the quote form again asking for a quote that already existed. The handler
        // stays as an idempotent safety net — it guards on the same statuses.
        if (lead is not null && lead.Status is LeadStatus.Nuevo or LeadStatus.Contactado or LeadStatus.Demostracion)
        {
            lead.ForceStatus(LeadStatus.Negociacion);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(quote.Id);
    }
}
