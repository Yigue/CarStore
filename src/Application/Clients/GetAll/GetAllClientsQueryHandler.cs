using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
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
        List<ClientResponse> clients = await context
            .Clients
            .AsNoTracking()
            .Select(client => new ClientResponse(
                client.Id,
                client.FirstName,
                client.LastName,
                client.DNI,
                client.Email,
                client.Phone,
                client.Address,
                client.Status,
                client.CreatedAt,
                client.UpdateAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ClientResponse>>(clients);
    }
}
