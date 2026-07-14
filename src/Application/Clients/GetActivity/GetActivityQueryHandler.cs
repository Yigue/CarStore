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

        const string clientAggregateType = "Client";
        const string saleAggregateType = "Sale";

        // Sale domain events are raised on the Sale aggregate, so they land in the outbox
        // keyed by SaleId, not ClientId. Resolve this client's sale ids so their events can
        // be folded into the same timeline — same outbox mechanism, just a wider aggregate
        // scope, instead of inventing a parallel audit trail.
        List<Guid> saleIds = await context.Sales
            .AsNoTracking()
            .Where(s => s.ClientId == query.ClientId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        // OutboxMessages has no global tenant filter — scope explicitly by the
        // resolved client's DealerId in addition to the aggregate identity.
        IQueryable<OutboxMessage> activityQuery = context.OutboxMessages
            .AsNoTracking()
            .Where(m => m.DealerId == client.DealerId
                     && ((m.AggregateId == query.ClientId && m.AggregateType == clientAggregateType)
                      || (m.AggregateType == saleAggregateType && m.AggregateId != null && saleIds.Contains(m.AggregateId.Value))));

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
