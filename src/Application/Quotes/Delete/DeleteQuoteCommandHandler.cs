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

        // REQ-QT-LEAK-001: release the reservation only for Pending quotes — the only
        // status that holds a live reservation. Mirrors RejectQuoteCommandHandler
        // (idempotent via Car.Release).
        if (quote.Status == QuoteStatus.Pending)
        {
            var car = await context.Cars
                .SingleOrDefaultAsync(c => c.Id == quote.CarId, cancellationToken);
            car?.Release(dateTimeProvider.UtcNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
