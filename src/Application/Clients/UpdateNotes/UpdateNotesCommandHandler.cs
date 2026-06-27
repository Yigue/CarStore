using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.UpdateNotes;

internal sealed class UpdateNotesCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateNotesCommand>
{
    public async Task<Result> Handle(UpdateNotesCommand command, CancellationToken cancellationToken)
    {
        Client? client = await context.Clients
            .SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (client is null)
            return Result.Failure(ClientErrors.NotFound(command.Id));

        Result updateResult = client.UpdateNotes(command.Notes, command.ActorId, dateTimeProvider.UtcNow);

        if (updateResult.IsFailure)
            return updateResult;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
