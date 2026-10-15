using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Suppliers.Get;

/// <summary>sfc B7.4: the caller's suppliers by name.</summary>
public sealed record GetSuppliersQuery : IQuery<IReadOnlyList<SupplierResponse>>;

public sealed record SupplierResponse(Guid Id, string Name, string? ContactEmail, string? Phone);

internal sealed class GetSuppliersQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetSuppliersQuery, IReadOnlyList<SupplierResponse>>
{
    public async Task<Result<IReadOnlyList<SupplierResponse>>> Handle(GetSuppliersQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<IReadOnlyList<SupplierResponse>>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        IReadOnlyList<SupplierResponse> rows = await context.Suppliers.AsNoTracking()
            .Where(s => s.DealerId == dealerId)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierResponse(s.Id, s.Name, s.ContactEmail, s.Phone))
            .ToListAsync(cancellationToken);
        return Result.Success(rows);
    }
}
