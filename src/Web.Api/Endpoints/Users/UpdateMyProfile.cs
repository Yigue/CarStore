using Application.Users.Commands.UpdateMyProfile;
using Application.Users.GetById;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class UpdateMyProfile : IEndpoint
{
    public sealed record Request(string FirstName, string LastName, string? Phone);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/me", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateMyProfileCommand(
                request.FirstName,
                request.LastName,
                request.Phone);

            Result<UserResponse> result = await sender.Send(command, cancellationToken);

            return result.Match(
                profile => Results.Ok(profile),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Users)
        .WithName("UpdateMyProfile")
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
