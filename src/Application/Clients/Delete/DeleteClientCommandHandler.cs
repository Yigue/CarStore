using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.Delete;

internal sealed class DeleteClientCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteClientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(DeleteClientCommand command, CancellationToken cancellationToken)
    {
        Client? client = await context.Clients
            .SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (client is null)
        {
            return Result.Failure<Guid>(ClientErrors.NotFound(command.Id));
        }

        context.Clients.Remove(client);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(client.Id);
    }
}
