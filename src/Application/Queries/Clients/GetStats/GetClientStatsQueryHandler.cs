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

        // ADR-6 / REQ-CRM-KPI-001: "Total Clientes" and ActiveCount must reflect only real
        // customers (Active + VIP). Prospect/Lost are auto-created CRM funnel shells and
        // must not inflate these KPIs — they are surfaced separately below.
        var activeOrVipCount = clients.Count(c => c.Status is ClientStatus.Active or ClientStatus.VIP);
        var totalCount = activeOrVipCount;
        var activeCount = activeOrVipCount;

        // Group by status as a proxy for "source" since there's no AcquisitionSource field
        // The design doc mentions bySource but Client entity doesn't have that field
        var bySource = clients
            .GroupBy(c => c.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var recentCount = clients.Count(c => c.CreatedAt > DateTime.UtcNow.AddDays(-30));
        var prospectCount = clients.Count(c => c.Status == ClientStatus.Prospect);
        var lostCount = clients.Count(c => c.Status == ClientStatus.Lost);

        var response = new ClientStatsResponse(
            totalCount,
            bySource,
            recentCount,
            activeCount,
            prospectCount,
            lostCount);

        return Result.Success(response);
    }
}