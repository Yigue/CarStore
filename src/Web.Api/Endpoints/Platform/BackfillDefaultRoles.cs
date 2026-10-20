using Application.Abstractions.Authentication;
using Application.Platform.Backfill.BackfillDefaultRoles;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Platform;

/// <summary>
/// spr PR 4.1 (SPR-18, D6): platform-admin backfill that converges every dealer's
/// Admin/Empleado/Cliente roles onto <c>DefaultRoleCatalog</c>. <c>dryRun=true</c> previews the
/// per-dealer drift without writing; <c>dryRun=false</c> creates missing roles and fully syncs
/// existing ones. Pattern follows <c>Endpoints/Cars/BackfillSaleCompletedCarStatus.cs</c>.
/// </summary>
internal sealed class BackfillDefaultRoles : IEndpoint
{
    public sealed class Request
    {
        public bool DryRun { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("platform/backfill/default-roles", async (
            Request request,
            IUserContext userContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new BackfillDefaultRolesCommand(
                DryRun: request.DryRun,
                ActorUserId: userContext.UserId);

            Result<BackfillDefaultRolesResult> result = await sender.Send(command, cancellationToken);

            return result.Match(
                value => Results.Ok(new
                {
                    action = value.Action.ToString(),
                    dealersAffected = value.DealersAffected,
                    diffs = value.Diffs,
                }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.RolesBackfill)
        .WithTags(Tags.Platform)
        .WithName("BackfillDefaultRoles")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
