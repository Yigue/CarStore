using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Accept;

internal sealed class AcceptQuoteCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AcceptQuoteCommand>
{
    public async Task<Result> Handle(AcceptQuoteCommand command, CancellationToken cancellationToken)
    {
        var quote = await context.Quotes
            .SingleOrDefaultAsync(q => q.Id == command.QuoteId, cancellationToken);
        
        if (quote is null)
            return Result.Failure(QuoteErrors.NotFound(command.QuoteId));
        
        if (quote.ValidUntil < dateTimeProvider.UtcNow)
            return Result.Failure(QuoteErrors.Expired(command.QuoteId));
        
        quote.Accept();
        
        await context.SaveChangesAsync(cancellationToken);
        
        return Result.Success();
    }
}

