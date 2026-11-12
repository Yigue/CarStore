using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.Create;

internal sealed class CreateClientCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService tenantService)
    : ICommandHandler<CreateClientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var dni = command.DNI.Trim();
        var exists = await context.Clients
            .AnyAsync(c => (!tenantService.HasTenant || c.DealerId == tenantService.DealerId) && c.DNI == dni, cancellationToken);

        if (exists)
        {
            return Result.Failure<Guid>(ClientErrors.DniConflict(dni));
        }

        var client = new Client(
            tenantService.DealerId,
            command.FirstName,
            command.LastName,
            dni,
            command.Email,
            command.Phone,
            command.Address,
            dateTimeProvider.UtcNow,
            command.Type,
            city: command.City,
            zipCode: command.ZipCode,
            notes: command.Notes);

        context.Clients.Add(client);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<Guid>(ClientErrors.DniConflict(dni));
        }

        return Result.Success(client.Id);
    }
}
