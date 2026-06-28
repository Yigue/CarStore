using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Clients.GetAll;
using Application.Clients.Projections;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.GetDeleted;

internal sealed class GetDeletedClientsQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetDeletedClientsQuery, PaginatedResult<ClientResponse>>
{
    public async Task<Result<PaginatedResult<ClientResponse>>> Handle(
        GetDeletedClientsQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = context.Clients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(c => c.Sales)
            .Where(c => c.IsDeleted && 
                        (!tenantService.HasTenant || c.DealerId == tenantService.DealerId));

        int totalCount = await dbQuery.CountAsync(cancellationToken);

        var clients = await dbQuery
            .OrderByDescending(c => c.DeletedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = clients.Select(ClientResponseMapper.Map).ToList();

        return Result.Success(new PaginatedResult<ClientResponse>(
            items,
            totalCount,
            query.Page,
            query.PageSize));
    }
}
