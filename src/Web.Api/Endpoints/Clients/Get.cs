using Application.Clients.GetAll;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;

namespace Web.Api.Endpoints.Clients;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients", async (
            ISender sender,
            string? search,
            string? status,
            string? type,
            string? source,
            Guid? assignedAgentId,
            DateTime? createdFrom,
            DateTime? createdTo,
            decimal? totalSalesMin,
            decimal? totalSalesMax,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            var query = new GetAllClientsQuery(
                search,
                status,
                type,
                source,
                assignedAgentId,
                createdFrom?.ToUtc(),
                createdTo?.ToUtc(),
                totalSalesMin,
                totalSalesMax,
                page,
                pageSize);

            Result<PaginatedResult<ClientResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                paginated => Results.Ok(paginated),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.ClientsRead)
        .WithTags(Tags.Clients)
        .WithName("GetAllClients")
        .Produces<PaginatedResult<ClientResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
