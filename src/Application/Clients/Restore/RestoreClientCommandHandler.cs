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

        Result result = client.Restore(userContext.UserId, dateTimeProvider.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(client.Id);
    }
}
