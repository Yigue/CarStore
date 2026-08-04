using Application.Clients.GetAll;
using Application.Queries.Clients.Search;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Clients.Search;

public sealed class SearchClients : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/search", async (
            [FromQuery] string q,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var query = new SearchClientsQuery { SearchTerm = q };
                var result = await sender.Send(query, cancellationToken);

                return result.Match(
                    data => Results.Ok(data),
                    CustomResults.Problem);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.ToString(), statusCode: 500);
            }
        })
        .HasPermission("clients:read")
        .WithTags(Tags.Clients)
        .WithName("SearchClients")
        .Produces<IEnumerable<ClientResponse>>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}