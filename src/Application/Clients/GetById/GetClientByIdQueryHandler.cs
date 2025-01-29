using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.GetAll;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.GetById;

internal sealed class GetClientByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetClientByIdQuery, ClientResponse>
{
    public async Task<Result<ClientResponse>> Handle(
        GetClientByIdQuery query,
        CancellationToken cancellationToken)
    {
        ClientResponse? client = await context
            .Clients
            .AsNoTracking()
            .Where(c => c.Id == query.Id)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (client is null)
        {
            return Result.Failure<ClientResponse>(ClientErrors.NotFound(query.Id));
        }

        return Result.Success(client);
    }
}
