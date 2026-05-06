using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.GetAll;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Clients.Search;

internal sealed class SearchClientsQueryHandler
    : IQueryHandler<SearchClientsQuery, IEnumerable<ClientResponse>>
{
    private readonly IApplicationDbContext _context;

    public SearchClientsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IEnumerable<ClientResponse>>> Handle(
        SearchClientsQuery query,
        CancellationToken cancellationToken)
    {
        var searchTerm = query.SearchTerm.ToLower();

        var clients = await _context.Clients
            .AsNoTracking()
            .Where(c => c.FirstName.ToLower().Contains(searchTerm) ||
                        c.LastName.ToLower().Contains(searchTerm) ||
                        c.Email.Value.ToLower().Contains(searchTerm))
            .Take(50)
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