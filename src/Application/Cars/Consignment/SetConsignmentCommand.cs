using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Cars;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Consignment;

/// <summary>
/// sfc B7.1 / RN-036: puts a car on consignment for one of the dealer's clients, or ends the consignment when
/// <paramref name="OwnerClientId"/> is null.
/// </summary>
public sealed record SetConsignmentCommand(Guid CarId, Guid? OwnerClientId, decimal? CommissionRate) : ICommand;

internal sealed class SetConsignmentCommandHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : ICommandHandler<SetConsignmentCommand>
{
    public async Task<Result> Handle(SetConsignmentCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return guard;

        Guid dealerId = tenantService.DealerId;
        Car? car = await context.Cars.FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);
        if (car is null || car.DealerId != dealerId)
            return Result.Failure(CarErrors.NotFound(command.CarId));

        if (command.OwnerClientId is not { } ownerId)
        {
            car.EndConsignment();
        }
        else
        {
            if (!await context.Clients.AnyAsync(c => c.Id == ownerId && c.DealerId == dealerId, cancellationToken))
                return Result.Failure(ClientErrors.NotFound(ownerId));

            Result placed = car.PlaceOnConsignment(ownerId, command.CommissionRate ?? 0m);
            if (placed.IsFailure)
                return placed;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
