using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.Projections;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.GetAll;

internal sealed class GetAllClientsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllClientsQuery, IReadOnlyList<ClientResponse>>
{
    public async Task<Result<IReadOnlyList<ClientResponse>>> Handle(
        GetAllClientsQuery query,
        CancellationToken cancellationToken)
    {
        List<Client> clients = await context.Clients
            .AsNoTracking()
            .Include(c => c.Sales)
            .ToListAsync(cancellationToken);

        List<ClientResponse> responses = clients
            .Select(ClientResponseMapper.Map)
            .ToList();

        return Result.Success<IReadOnlyList<ClientResponse>>(responses);
    }
}
