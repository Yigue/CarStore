using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Leads;
using Domain.Quotes;
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

        // D-1: solo se puede cotizar un vehículo Disponible. Si ya está reservado
        // (por otra cotización activa) o vendido -> 409.
        if (car.ServiceCar != StatusServiceCar.Disponible)
        {
            return Result.Failure<Guid>(CarErrors.NotAvailable(command.CarId));
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
            // IgnoreQueryFilters: the default LeadConfiguration query filter hides
            // Archivado leads entirely. We need to still resolve them here so the
            // gate below can return the precise LeadNotQuotable error instead of a
            // misleading NotFound for a lead that does exist but is archived.
            lead = await context.Leads
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(l => l.Id == leadId, cancellationToken);

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
        else
        {
            return Result.Failure<Guid>(new Error(
                "Quotes.MissingParty",
                "A quote must reference either a client or a lead.",
                ErrorType.Validation));
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

        // D-1: reservar el vehículo en la misma transacción que la cotización.
        car.Reserve(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(quote.Id);
    }
}
