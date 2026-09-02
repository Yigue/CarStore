using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Reject;

internal sealed class RejectQuoteCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RejectQuoteCommand>
{
    public async Task<Result> Handle(RejectQuoteCommand command, CancellationToken cancellationToken)
    {
        var quote = await context.Quotes
            .SingleOrDefaultAsync(q => q.Id == command.QuoteId, cancellationToken);
        
        if (quote is null)
            return Result.Failure(QuoteErrors.NotFound(command.QuoteId));

        // Quote.Reject only accepts a Pending quote, and a Pending quote holds no reservation
        // since the hold moved to acceptance. The car is deliberately left alone: releasing it
        // here would free a unit that a competing ACCEPTED quote is holding.
        quote.Reject(command.Reason, dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

