using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Delete;

internal sealed class DeleteQuoteCommandHandler(
    IApplicationDbContext context)
    : ICommandHandler<DeleteQuoteCommand>
{
    public async Task<Result> Handle(DeleteQuoteCommand command, CancellationToken cancellationToken)
    {
        var quote = await context.Quotes
            .SingleOrDefaultAsync(q => q.Id == command.QuoteId, cancellationToken);
        
        if (quote is null)
            return Result.Failure(QuoteErrors.NotFound(command.QuoteId));
        
        // Only allow deletion of pending quotes
        if (quote.Status != QuoteStatus.Pending)
            return Result.Failure(QuoteErrors.CannotDeleteNonPendingQuote(command.QuoteId));
        
        context.Quotes.Remove(quote);
        
        await context.SaveChangesAsync(cancellationToken);
        
        return Result.Success();
    }
}

