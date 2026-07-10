using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.Delete;

internal sealed class DeleteClientCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<DeleteClientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(DeleteClientCommand command, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: detect already-deleted clients for idempotent soft-delete
        Client? client = await context.Clients
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (client is null)
            return Result.Failure<Guid>(ClientErrors.NotFound(command.Id));

        // Idempotent: if already deleted, return success without raising event
        if (client.IsDeleted)
            return Result.Success(client.Id);

        client.Delete(userContext.UserId, dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(client.Id);
    }
}
