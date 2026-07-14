using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Queries.GetAgents;

internal sealed class GetAgentsQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetAgentsQuery, List<AgentResponse>>
{
    // Cliente/Invitado are not staff and don't get assigned to sales. SuperAdmin is
    // excluded implicitly: it has DealerId = Guid.Empty (ADR-1) and never matches the
    // tenant filter below, so it never leaks into a dealer-scoped agents list.
    private static readonly UserRole[] AgentRoles = [UserRole.Admin, UserRole.Empleado];

    public async Task<Result<List<AgentResponse>>> Handle(GetAgentsQuery query, CancellationToken cancellationToken)
    {
        var dealerId = tenantService.DealerId;

        var agents = await context.Users
            .Where(u => u.DealerId == dealerId)
            .Where(u => u.IsActive)
            .Where(u => AgentRoles.Contains(u.Role))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new AgentResponse(
                u.Id,
                u.FirstName,
                u.LastName,
                u.FirstName + " " + u.LastName,
                u.Role.ToString()))
            .ToListAsync(cancellationToken);

        return Result.Success(agents);
    }
}
