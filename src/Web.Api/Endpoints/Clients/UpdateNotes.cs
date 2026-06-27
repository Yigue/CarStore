using Application.Clients.GetById;
using Application.Clients.GetAll;
using Application.Clients.UpdateNotes;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Web.Api.Endpoints.Clients;

internal sealed class UpdateNotes : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("clients/{id}/notes",
            async (Guid id, Request request, ISender sender, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            {
                Guid? actorId = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
                    ? parsed
                    : null;

                var command = new UpdateNotesCommand(id, request.Notes, actorId);

                Result updateResult = await sender.Send(command, cancellationToken);

                if (updateResult.IsFailure)
                    return CustomResults.Problem(updateResult);

                Result<ClientResponse> fetchResult = await sender.Send(new GetClientByIdQuery(id), cancellationToken);

                return fetchResult.Match(
                    client => Results.Ok(client),
                    CustomResults.Problem);
            })
        .HasPermission(Permissions.ClientsWrite)
        .WithTags(Tags.Clients)
        .WithName("UpdateClientNotes")
        .Produces<ClientResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record Request(string? Notes);
}
