using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Quotes;
using Domain.Quotes.Attributes;
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

        // The exclusivity rule. A car can carry any number of competing offers — that is the
        // normal shape of a negotiation — but accepting one is the dealership committing the
        // unit, and a unit can only be committed once. Creating quotes used to reserve the car,
        // which enforced this far too early and blocked the second offer outright.
        bool alreadyCommitted = await context.Quotes
            .AnyAsync(
                q => q.CarId == quote.CarId
                     && q.Id != quote.Id
                     && q.Status == QuoteStatus.Accepted,
                cancellationToken);

        if (alreadyCommitted)
            return Result.Failure(QuoteErrors.CarAlreadyCommitted(quote.CarId));

        quote.Accept(dateTimeProvider.UtcNow);

        // Hold the unit for this deal. Competing Pending quotes are deliberately left alone:
        // refusing their acceptance is the system's job, rejecting them is the operator's.
        Car? car = await context.Cars
            .SingleOrDefaultAsync(c => c.Id == quote.CarId, cancellationToken);

        // Reserve() throws on anything but Disponible, so a car already held by this same deal
        // (a retry, a reservation carried over from a sale in progress) must not blow up here.
        if (car is not null && car.ServiceCar == StatusServiceCar.Disponible)
        {
            car.Reserve(dateTimeProvider.UtcNow);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

