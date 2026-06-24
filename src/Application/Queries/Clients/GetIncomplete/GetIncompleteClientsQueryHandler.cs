using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Clients.GetIncomplete;

internal sealed class GetIncompleteClientsQueryHandler
    : IQueryHandler<GetIncompleteClientsQuery, IEnumerable<IncompleteClientResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetIncompleteClientsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IEnumerable<IncompleteClientResponse>>> Handle(
        GetIncompleteClientsQuery query,
        CancellationToken cancellationToken)
    {
        // Auto-conversion seeds a placeholder DNI of the form "n" + Guid("N") (33 chars).
        var clients = await _context.Clients
            .AsNoTracking()
            .Where(c => c.DNI.StartsWith("n") && c.DNI.Length == 33)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new IncompleteClientResponse(
                c.Id,
                c.FirstName,
                c.LastName,
                c.Email.Value))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<IncompleteClientResponse>>(clients);
    }
}
