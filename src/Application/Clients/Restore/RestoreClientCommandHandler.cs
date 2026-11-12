using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.Restore;

internal sealed class RestoreClientCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService tenantService)
    : ICommandHandler<RestoreClientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RestoreClientCommand command, CancellationToken cancellationToken)
    {
        Client? client = await context.Clients
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == command.Id && 
                                      (!tenantService.HasTenant || c.DealerId == tenantService.DealerId), cancellationToken);

        if (client is null)
        {
            return Result.Failure<Guid>(ClientErrors.NotFound(command.Id));
        }

        // Check if an active client already exists with this DNI in the same dealer
        var dniConflict = await context.Clients
            .AnyAsync(c => (!tenantService.HasTenant || c.DealerId == tenantService.DealerId) && c.Id != client.Id && c.DNI == client.DNI, cancellationToken);

        if (dniConflict)
        {
            return Result.Failure<Guid>(ClientErrors.DniConflict(client.DNI));
        }

        Result result = client.Restore(userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<Guid>(ClientErrors.DniConflict(client.DNI));
        }

        return Result.Success(client.Id);
    }
}
