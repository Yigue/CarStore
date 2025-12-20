using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Update;

internal sealed class UpdateQuoteCommandHandler(
    IApplicationDbContext context)
    : ICommandHandler<UpdateQuoteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateQuoteCommand command, CancellationToken cancellationToken)
    {
        Quote? quote = await context.Quotes
            .FirstOrDefaultAsync(q => q.Id == command.Id, cancellationToken);

        if (quote is null)
        {
            return Result.Failure<Guid>(QuoteErrors.NotFound(command.Id));
        }

        quote.Update(
            command.ProposedPrice,
            command.ValidUntil,
            command.Comments);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(quote.Id);
    }
}
