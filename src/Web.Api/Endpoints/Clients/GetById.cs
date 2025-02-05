using Application.Clients.GetAll;
using Application.Clients.GetById;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Clients;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/{id}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetClientByIdQuery(id);

            Result<ClientResponse> result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
            {
                return Results.NotFound(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithTags("Clients");
    }
}
