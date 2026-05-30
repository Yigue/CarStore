using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.GetAll;

internal sealed class GetLeadsQueryHandler(
    IApplicationDbContext context
) : IQueryHandler<GetLeadsQuery, List<LeadResponse>>
{
    public async Task<Result<List<LeadResponse>>> Handle(GetLeadsQuery query, CancellationToken cancellationToken)
    {
        var leadsQuery = context.Leads
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .AsQueryable();

        if (query.Status.HasValue)
            leadsQuery = leadsQuery.Where(l => l.Status == query.Status.Value);

        var dbResults = await (from l in leadsQuery
            join u in context.Users.IgnoreQueryFilters() on l.AssignedAgentId equals (Guid?)u.Id into agentJoin
            from agent in agentJoin.DefaultIfEmpty()
            select new 
            {
                l.Id,
                l.ClientName,
                Email = EF.Property<string>(l, "Email"),
                l.Phone,
                l.Status,
                l.AssignedAgentId,
                AgentName = agent != null ? agent.FirstName + " " + agent.LastName : null,
                l.Notes,
                l.Source,
                l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var results = dbResults.Select(l => new LeadResponse(
            l.Id,
            l.ClientName,
            l.Email,
            l.Phone,
            l.Status,
            l.Status.ToString(),
            l.AssignedAgentId,
            l.AgentName,
            l.Notes,
            l.Source.ToString(),
            l.CreatedAt
        )).ToList();

        return Result.Success(results);
    }
}
