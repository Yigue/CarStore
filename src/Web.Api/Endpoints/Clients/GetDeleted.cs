using Application.Clients.GetDeleted;
using Application.Clients.GetAll;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using System.Threading;

namespace Web.Api.Endpoints.Clients;

internal sealed class GetDeleted : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/deleted", async (
            ISender sender,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            var query = new GetDeletedClientsQuery(page, pageSize);

            Result<PaginatedResult<ClientResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                paginated => Results.Ok(paginated),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.ClientsDelete)
        .WithTags(Tags.Clients)
        .WithName("GetDeletedClients")
        .Produces<PaginatedResult<ClientResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
