using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Delete;

internal sealed class DeleteQuoteCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteQuoteCommand>
{
    public async Task<Result> Handle(DeleteQuoteCommand command, CancellationToken cancellationToken)
    {
        Quote? quote = await context.Quotes
            .SingleOrDefaultAsync(q => q.Id == command.QuoteId, cancellationToken);

        if (quote is null)
            return Result.Failure(QuoteErrors.NotFound(command.QuoteId));

        // Soft delete: the row is retained and excluded from default queries via the global
        // query filter. This is an admin action and is allowed regardless of quote status.
        quote.Delete(dateTimeProvider.UtcNow);

        // Release only for an ACCEPTED quote — the only status that holds a live reservation
        // since the hold moved from creation to acceptance. A Pending quote is one offer among
        // several; releasing on its behalf would free a car another buyer already committed to.
        // Idempotent via Car.Release, which no-ops on anything but Reservado.
        if (quote.Status == QuoteStatus.Accepted)
        {
            var car = await context.Cars
                .SingleOrDefaultAsync(c => c.Id == quote.CarId, cancellationToken);
            car?.Release(dateTimeProvider.UtcNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
