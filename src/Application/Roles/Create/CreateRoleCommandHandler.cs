using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.Create;

internal sealed class CreateRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<CreateRoleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        string name = command.Name.Trim();

        // Role.SetDealer stamps the creating tenant, and the global query filter keeps this
        // comparison inside the dealership: two dealerships may each have a "Vendedor".
        bool nameTaken = await context.Roles
            .AnyAsync(r => r.Name == name, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure<Guid>(RoleErrors.NameAlreadyExists(name));
        }

        // Validate BEFORE creating anything. A permission the API never checks would be stored,
        // rendered as granted on the matrix, and silently never match.
        foreach (string permission in command.Permissions.Distinct(StringComparer.Ordinal))
        {
            if (!PermissionCatalog.IsGrantable(permission))
            {
                return Result.Failure<Guid>(RoleErrors.PermissionNotGrantable(permission));
            }
        }

        var role = new Role(tenantService.DealerId, name, command.Description?.Trim() ?? string.Empty);

        foreach (string permission in command.Permissions.Distinct(StringComparer.Ordinal))
        {
            role.AddPermission(permission);
        }

        context.Roles.Add(role);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(role.Id);
    }
}
