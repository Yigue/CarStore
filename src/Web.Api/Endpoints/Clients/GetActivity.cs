using Application.Clients.GetActivity;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Clients;

internal sealed class GetActivity : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/{id:guid}/activity",
            async (Guid id, ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = 50) =>
            {
                var query = new GetActivityQuery(id, page, pageSize);

                Result<ClientActivityResponse> result = await sender.Send(query, cancellationToken);

                return result.Match(
                    activity => Results.Ok(activity),
                    CustomResults.Problem);
            })
        .HasPermission(Permissions.ClientsRead)
        .WithTags(Tags.Clients)
        .WithName("GetClientActivity")
        .Produces<ClientActivityResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
