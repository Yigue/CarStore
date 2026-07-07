using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DealerSettings.Queries.GetHostName;

/// <summary>
/// Handler for <see cref="GetHostNameQuery"/>.
/// Returns only the host-identity fields of the current tenant's DealerSettings.
/// Uses <c>IgnoreQueryFilters()</c> consistently with the write side so the query
/// works correctly even if <c>ICurrentTenantService.HasTenant</c> is false for
/// some edge-case caller.
/// </summary>
internal sealed class GetHostNameQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetHostNameQuery, HostNameResponse>
{
    public async Task<Result<HostNameResponse>> Handle(
        GetHostNameQuery query,
        CancellationToken cancellationToken)
    {
        HostNameResponse? response = await context.DealerSettings
            .IgnoreQueryFilters()
            .Where(s => s.DealerId == tenantService.DealerId)
            .Select(s => new HostNameResponse(s.HostName, s.Slug, s.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

        if (response is null)
        {
            return Result.Failure<HostNameResponse>(DealerSettingsErrors.NotFound);
        }

        return response;
    }
}
