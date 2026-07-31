using Application.Clients.Restore;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using System;
using System.Threading;

namespace Web.Api.Endpoints.Clients;

internal sealed class Restore : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("clients/{id:guid}/restore", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new RestoreClientCommand(id);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                clientId => Results.Ok(clientId),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.ClientsDelete)
        .WithTags(Tags.Clients)
        .WithName("RestoreClient")
        .Produces<Guid>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
