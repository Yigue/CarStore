using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Clients.GetStats;

internal sealed class GetClientStatsQueryHandler
    : IQueryHandler<GetClientStatsQuery, ClientStatsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetClientStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClientStatsResponse>> Handle(
        GetClientStatsQuery query,
        CancellationToken cancellationToken)
    {
        var clients = await _context.Clients
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var totalCount = clients.Count;

        // Group by status as a proxy for "source" since there's no AcquisitionSource field
        // The design doc mentions bySource but Client entity doesn't have that field
        var bySource = clients
            .GroupBy(c => c.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var recentCount = clients.Count(c => c.CreatedAt > DateTime.UtcNow.AddDays(-30));
        var activeCount = clients.Count(c => c.Status == ClientStatus.Active);

        var response = new ClientStatsResponse(
            totalCount,
            bySource,
            recentCount,
            activeCount);

        return Result.Success(response);
    }
}