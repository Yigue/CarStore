using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.GetAll;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Clients.GetTop;

internal sealed class GetTopClientsQueryHandler
    : IQueryHandler<GetTopClientsQuery, IEnumerable<ClientWithRevenueResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetTopClientsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IEnumerable<ClientWithRevenueResponse>>> Handle(
        GetTopClientsQuery query,
        CancellationToken cancellationToken)
    {
        var clients = await _context.Clients
            .AsNoTracking()
            .Include(c => c.Sales)
            .ToListAsync(cancellationToken);

        var clientsWithRevenue = clients
            .Select(c => new ClientWithRevenueResponse(
                c.Id,
                c.FirstName,
                c.LastName,
                c.DNI,
                c.Email.Value,
                c.Phone,
                c.Address,
                c.Status,
                c.CreatedAt,
                c.UpdateAt,
                c.Sales.Sum(s => s.FinalPrice.Amount)))
            .OrderByDescending(c => c.TotalSalesAmount)
            .Take(query.Limit)
            .ToList();

        return Result.Success<IEnumerable<ClientWithRevenueResponse>>(clientsWithRevenue);
    }
}