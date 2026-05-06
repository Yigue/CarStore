using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.GetAll;
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

        var clients = await _context.Clients
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(cappedLimit)
            .Select(c => new ClientResponse(
                c.Id,
                c.FirstName,
                c.LastName,
                c.DNI,
                c.Email.Value,
                c.Phone,
                c.Address,
                c.Status,
                c.CreatedAt,
                c.UpdateAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<ClientResponse>>(clients);
    }
}