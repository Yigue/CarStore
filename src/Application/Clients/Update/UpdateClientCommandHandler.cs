using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.Update;

internal sealed class UpdateClientCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateClientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateClientCommand command, CancellationToken cancellationToken)
    {
        Client? client = await context.Clients
            .SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (client is null)
        {
            return Result.Failure<Guid>(ClientErrors.NotFound(command.Id));
        }

        client.Update(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Phone,
            command.Address);
        
        // Handle status change using domain methods
        if (command.Status == ClientStatus.Active && client.Status == ClientStatus.Inactive)
        {
            client.Activate();
        }
        else if (command.Status == ClientStatus.Inactive && client.Status == ClientStatus.Active)
        {
            client.Deactivate();
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(client.Id);
    }
}
