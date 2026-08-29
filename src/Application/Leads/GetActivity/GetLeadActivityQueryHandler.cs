using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.GetActivity;

internal sealed class GetLeadActivityQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetLeadActivityQuery, LeadActivityResponse>
{
    public async Task<Result<LeadActivityResponse>> Handle(
        GetLeadActivityQuery query,
        CancellationToken cancellationToken)
    {
        // Tenant isolation rides on the global filter over Leads: a lead belonging to another
        // dealership resolves to null and answers 404, so the activity query below can never be
        // reached with someone else's id.
        bool leadExists = await context.Leads
            .AsNoTracking()
            .AnyAsync(l => l.Id == query.LeadId, cancellationToken);

        if (!leadExists)
        {
            return Result.Failure<LeadActivityResponse>(LeadErrors.NotFound(query.LeadId));
        }

        IQueryable<LeadActivity> activities = context.LeadActivities
            .AsNoTracking()
            .Where(a => a.LeadId == query.LeadId);

        int totalCount = await activities.CountAsync(cancellationToken);

        List<LeadActivityEntry> items = await activities
            .OrderByDescending(a => a.OccurredAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new LeadActivityEntry(
                a.Id,
                a.Type,
                a.Description,
                a.RelatedEntityId,
                a.RelatedEntityType,
                a.ActorUserId,
                a.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new LeadActivityResponse(items, totalCount));
    }
}
