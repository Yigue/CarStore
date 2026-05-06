using Application.Queries.Users.GetAll;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Users.GetAll;

public sealed class GetAllUsers : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetAllUsersQuery(), cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Users)
        .WithName("GetAllUsers")
        .Produces<IEnumerable<UserResponse>>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}