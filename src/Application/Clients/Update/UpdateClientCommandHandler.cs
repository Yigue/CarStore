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

        client.FirstName = command.FirstName;
        client.LastName = command.LastName;
        client.DNI = command.DNI;
        client.Email = command.Email;
        client.Phone = command.Phone;
        client.Address = command.Address;
        client.Status = command.Status;
        client.UpdateAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(client.Id);
    }
}
