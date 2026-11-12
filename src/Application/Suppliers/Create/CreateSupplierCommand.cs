using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Suppliers.Create;

/// <summary>sfc B7.4 / RN-039: registers a supplier for the caller's dealer. Returns its id.</summary>
public sealed record CreateSupplierCommand(string Name, string? ContactEmail, string? Phone) : ICommand<Guid>;

internal sealed class CreateSupplierCommandHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : ICommandHandler<CreateSupplierCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<Guid>(guard.Error);

        Result<Supplier> created = Supplier.Create(tenantService.DealerId, command.Name, command.ContactEmail, command.Phone);
        if (created.IsFailure)
            return Result.Failure<Guid>(created.Error);

        Guid dealerId = tenantService.DealerId;
        string normalized = created.Value.NameNormalized;
        if (await context.Suppliers.AnyAsync(s => s.DealerId == dealerId && s.NameNormalized == normalized, cancellationToken))
            return Result.Failure<Guid>(SupplierErrors.AlreadyExists(created.Value.Name));

        context.Suppliers.Add(created.Value);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the race on ux_suppliers_dealer_id_name_normalized.
            return Result.Failure<Guid>(SupplierErrors.AlreadyExists(created.Value.Name));
        }

        return Result.Success(created.Value.Id);
    }
}
