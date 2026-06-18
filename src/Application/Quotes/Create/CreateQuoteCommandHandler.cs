using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Clients;
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
        }
        else if (command.LeadId is { } leadId)
        {
            lead = await context.Leads
                .SingleOrDefaultAsync(l => l.Id == leadId, cancellationToken);

            if (lead is null)
            {
                return Result.Failure<Guid>(LeadErrors.NotFound(leadId));
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

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(quote.Id);
    }
}
