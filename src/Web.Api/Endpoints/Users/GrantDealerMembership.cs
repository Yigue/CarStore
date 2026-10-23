using Application.Users.Commands.GrantDealerMembership;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>sfc B6.3 / RN-043: grants an existing user access to the caller's dealer, in addition to
/// their home dealer. Does not invite/create a user.</summary>
internal sealed class GrantDealerMembership : IEndpoint
{
    public sealed record Request(Guid RoleId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/{userId:guid}/dealer-memberships", async (
            Guid userId,
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new GrantUserDealerMembershipCommand(userId, request.RoleId);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Ok(new { id }),
                CustomResults.Problem);
        })
        .HasPermission("CanManageUsers")
        .WithTags(Tags.Users)
        .WithName("GrantUserDealerMembership")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
