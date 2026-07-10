using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.GetAll;
using Application.Clients.Projections;
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
        Client? client = await context.Clients
            .AsNoTracking()
            .Include(c => c.Sales)
            .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);

        if (client is null)
        {
            return Result.Failure<ClientResponse>(ClientErrors.NotFound(query.Id));
        }

        return Result.Success(ClientResponseMapper.Map(client));
    }
}
