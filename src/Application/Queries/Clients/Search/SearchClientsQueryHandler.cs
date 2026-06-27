using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.GetAll;
using Application.Clients.Projections;
using Domain.Clients;
using Domain.Shared.ValueObjects;
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
        var rawTerm = query.SearchTerm ?? string.Empty;
        var searchTerm = rawTerm.ToLower();

        // Email is persisted through a value converter (EmailValueConverter), so EF Core
        // cannot translate a substring match on it to SQL. Names are matched by substring
        // in the database; the email is matched exactly when the term is a well-formed
        // address. Partial-email search is intentionally unsupported with this mapping.
        Email? emailTerm = TryParseEmail(rawTerm);

        List<Client> clients = await _context.Clients
            .AsNoTracking()
            .Include(c => c.Sales)
            .Where(c => searchTerm == string.Empty ||
                        c.FirstName.ToLower().Contains(searchTerm) ||
                        c.LastName.ToLower().Contains(searchTerm) ||
                        (emailTerm != null && c.Email == emailTerm))
            .Take(50)
            .ToListAsync(cancellationToken);

        IEnumerable<ClientResponse> responses = clients.Select(ClientResponseMapper.Map);
        return Result.Success<IEnumerable<ClientResponse>>(responses);
    }

    private static Email? TryParseEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return new Email(value);
        }
        catch (SharedKernel.DomainException)
        {
            return null;
        }
    }
}