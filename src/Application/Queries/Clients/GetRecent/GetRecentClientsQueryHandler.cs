using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.GetAll;
using Application.Clients.Projections;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Clients.GetRecent;

internal sealed class GetRecentClientsQueryHandler
    : IQueryHandler<GetRecentClientsQuery, IEnumerable<ClientResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetRecentClientsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IEnumerable<ClientResponse>>> Handle(
        GetRecentClientsQuery query,
        CancellationToken cancellationToken)
    {
        // Cap at 100 max
        var cappedLimit = Math.Min(query.Limit, 100);

        List<Client> clients = await _context.Clients
            .AsNoTracking()
            .Include(c => c.Sales)
            .OrderByDescending(c => c.CreatedAt)
            .Take(cappedLimit)
            .ToListAsync(cancellationToken);

        IEnumerable<ClientResponse> responses = clients.Select(ClientResponseMapper.Map);
        return Result.Success<IEnumerable<ClientResponse>>(responses);
    }
}