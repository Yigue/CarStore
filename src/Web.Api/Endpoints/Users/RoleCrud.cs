using Application.Roles.Create;
using Application.Roles.Delete;
using Application.Roles.Update;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// CFG-03: a dealership defines the roles its own structure needs.
///
/// <para>
/// The four fixed roles (Admin, Empleado, Cliente, Invitado) are a login classification, not an
/// org chart. A real dealership has a Gerente de Taller who must not see the financials and a
/// Vendedor Junior who may quote but not accept — neither of which any of the four expresses, so
/// everyone ended up an Admin.
/// </para>
/// </summary>
internal sealed class RoleCrud : IEndpoint
{
    public sealed record CreateRequest(string Name, string Description, IReadOnlyList<string> Permissions);

    public sealed record UpdateRequest(string Name, string Description, IReadOnlyList<string> Permissions);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("roles", async (
            CreateRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateRoleCommand(
                Name: request.Name,
                Description: request.Description,
                Permissions: request.Permissions ?? []);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/roles/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CanManageRoles)
        .WithTags(Tags.Users)
        .WithName("CreateRole")
        .Produces(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPut("roles/{roleId:guid}", async (
            Guid roleId,
            UpdateRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateRoleCommand(
                RoleId: roleId,
                Name: request.Name,
                Description: request.Description,
                Permissions: request.Permissions ?? []);

            Result result = await sender.Send(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CanManageRoles)
        .WithTags(Tags.Users)
        .WithName("UpdateRole")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapDelete("roles/{roleId:guid}", async (
            Guid roleId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new DeleteRoleCommand(roleId), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(Permissions.CanManageRoles)
        .WithTags(Tags.Users)
        .WithName("DeleteRole")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
