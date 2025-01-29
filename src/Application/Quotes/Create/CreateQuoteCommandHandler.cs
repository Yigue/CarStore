using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Clients;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.Create;

internal sealed class CreateQuoteCommandHandler(
    IApplicationDbContext context)
    : ICommandHandler<CreateQuoteCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .SingleOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));
        }

        Client? client = await context.Clients
            .SingleOrDefaultAsync(c => c.Id == command.ClientId, cancellationToken);

        if (client is null)
        {
            return Result.Failure<Guid>(ClientErrors.NotFound(command.ClientId));
        }

        var quote = new Quote(
            car,
            client,
            command.ProposedPrice,
            command.ValidUntil,
            command.Comments);

        context.Quotes.Add(quote);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(quote.Id);
    }
}
