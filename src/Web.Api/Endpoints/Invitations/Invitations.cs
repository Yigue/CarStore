using Application.Users.Invitations.AcceptInvitation;
using Application.Users.Invitations.InviteUser;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Invitations;

/// <summary>
/// sfc A4.2 / A14 / RN-031: <c>POST users/invitations</c> (<c>CanManageUsers</c>) emails a 7-day token;
/// <c>POST invitations/accept</c> (anonymous) consumes it and creates an already-verified user.
/// </summary>
internal sealed class Invitations : IEndpoint
{
    public sealed record InviteRequest(string Email, Guid RoleId);

    public sealed record AcceptRequest(string Token, string Password, string FirstName, string LastName);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/invitations", async (
                InviteRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result result = await sender.Send(new InviteUserCommand(request.Email, request.RoleId), cancellationToken);

                return result.Match(
                    () => Results.Ok(new { message = "Enviamos la invitación por email." }),
                    CustomResults.Problem);
            })
            .HasPermission("CanManageUsers")
            .WithTags(Tags.Users)
            .WithName("InviteUser")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("invitations/accept", async (
                AcceptRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new AcceptInvitationCommand(
                    Token: request.Token,
                    Password: request.Password,
                    FirstName: request.FirstName,
                    LastName: request.LastName);
                Result<Guid> result = await sender.Send(command, cancellationToken);

                return result.Match(
                    id => Results.Created($"/users/{id}", new { id }),
                    CustomResults.Problem);
            })
            .WithTags(Tags.Users)
            .WithName("AcceptInvitation")
            .AllowAnonymous()
            .RequireRateLimiting("login")
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
