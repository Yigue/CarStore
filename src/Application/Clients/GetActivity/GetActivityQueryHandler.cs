using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients;
using Domain.Shared;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.GetActivity;

internal sealed class GetActivityQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetActivityQuery, ClientActivityResponse>
{
    public async Task<Result<ClientActivityResponse>> Handle(
        GetActivityQuery query,
        CancellationToken cancellationToken)
    {
        // Tenant isolation: context.Clients is globally filtered by DealerId, so a
        // client belonging to another tenant resolves to null → 404 (never leaks).
        Client? client = await context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.ClientId, cancellationToken);

        if (client is null)
        {
            return Result.Failure<ClientActivityResponse>(ClientErrors.NotFound(query.ClientId));
        }

        const string aggregateType = "Client";

        // OutboxMessages has no global tenant filter — scope explicitly by the
        // resolved client's DealerId in addition to the aggregate identity.
        IQueryable<OutboxMessage> activityQuery = context.OutboxMessages
            .AsNoTracking()
            .Where(m => m.AggregateId == query.ClientId
                     && m.AggregateType == aggregateType
                     && m.DealerId == client.DealerId);

        int totalCount = await activityQuery.CountAsync(cancellationToken);

        var items = await activityQuery
            .OrderByDescending(m => m.OccurredOnUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(m => new ActivityEntry(m.Id, m.Type, m.OccurredOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new ClientActivityResponse(items, totalCount));
    }
}
