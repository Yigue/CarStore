using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Update;

internal sealed class UpdateQuoteCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService? tenantService = null)
    : ICommandHandler<UpdateQuoteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateQuoteCommand command, CancellationToken cancellationToken)
    {
        if (tenantService is not null)
        {
            Result guard = TenantGuard.EnsureHasTenant(tenantService);
            if (guard.IsFailure)
            {
                return Result.Failure<Guid>(guard.Error);
            }
        }

        Quote? quote = await context.Quotes
            .FirstOrDefaultAsync(q => q.Id == command.Id, cancellationToken);
 
        if (quote is null)
        {
            return Result.Failure<Guid>(QuoteErrors.NotFound(command.Id));
        }

        if (tenantService is not null)
        {
            Result dealerCheck = TenantGuard.EnsureSameDealerOrNotFound(tenantService, quote.DealerId, QuoteErrors.NotFound(command.Id));
            if (dealerCheck.IsFailure)
            {
                return Result.Failure<Guid>(dealerCheck.Error);
            }
        }
 
        try
        {
            quote.Update(
                command.ProposedPrice,
                command.ValidUntil,
                command.Comments,
                dateTimeProvider.UtcNow);
        }
        catch (DomainException)
        {
            return Result.Failure<Guid>(QuoteErrors.AlreadyProcessed(quote.Id));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(quote.Id);
    }
}
