using Application.Abstractions;
using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

internal sealed class RoundRobinLeadAllocator(
    IApplicationDbContext context
) : IRoundRobinLeadAllocator
{
    public async Task<Guid?> AllocateAsync(Guid dealerId, CancellationToken ct)
    {
        // Fetch active sales agents for this dealer, ordered by Id for determinism
        // User does not have IsActive — filter by DealerId only
        var activeAgents = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.DealerId == dealerId)
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (activeAgents.Count == 0)
            return null;

        // Fetch dealer settings for the round-robin pointer
        var settings = await context.DealerSettings
            .FirstOrDefaultAsync(s => s.DealerId == dealerId, ct);

        if (settings is null)
            return activeAgents[0];

        // Read current index BEFORE incrementing
        var currentIndex = settings.LastAssignedAgentIndex;
        var nextAgentId = activeAgents[currentIndex % activeAgents.Count];

        // Increment pointer — persisted atomically with the Lead save (same UoW)
        settings.IncrementAgentIndex();

        return nextAgentId;
    }
}
