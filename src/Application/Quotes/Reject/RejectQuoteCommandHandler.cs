using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Reject;

internal sealed class RejectQuoteCommandHandler(
    IApplicationDbContext context)
    : ICommandHandler<RejectQuoteCommand>
{
    public async Task<Result> Handle(RejectQuoteCommand command, CancellationToken cancellationToken)
    {
        var quote = await context.Quotes
            .SingleOrDefaultAsync(q => q.Id == command.QuoteId, cancellationToken);
        
        if (quote is null)
            return Result.Failure(QuoteErrors.NotFound(command.QuoteId));
        
        quote.Reject(command.Reason);
        
        await context.SaveChangesAsync(cancellationToken);
        
        return Result.Success();
    }
}

