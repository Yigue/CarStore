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
            // No local catch-all: it bypassed GlobalExceptionHandler and returned
            // ex.ToString() — full stack trace and internal paths — to the caller.
            // Unhandled exceptions belong to GlobalExceptionHandler, which logs the
            // detail server-side and answers with an opaque 500 ProblemDetails.
            var query = new SearchClientsQuery { SearchTerm = q };
            var result = await sender.Send(query, cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .HasPermission("clients:read")
        .WithTags(Tags.Clients)
        .WithName("SearchClients")
        .Produces<IEnumerable<ClientResponse>>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}