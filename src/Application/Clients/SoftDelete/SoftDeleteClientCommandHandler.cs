using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.SoftDelete;

/// <summary>
/// Soft-deletes a client (idempotent). If the client is already deleted, returns success without
/// raising a domain event. Tenant-scoped via the global query filter.
/// </summary>
internal sealed class SoftDeleteClientCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SoftDeleteClientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SoftDeleteClientCommand command, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters to detect already-deleted clients for idempotent behaviour
        Client? client = await context.Clients
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (client is null)
            return Result.Failure<Guid>(ClientErrors.NotFound(command.Id));

        // Idempotent: already deleted — return success without event
        if (client.IsDeleted)
            return Result.Success(client.Id);

        client.Delete(userContext.UserId, dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(client.Id);
    }
}
