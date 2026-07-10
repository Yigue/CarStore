using Application.Leads.UpdateNotes;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class UpdateNotes : IEndpoint
{
    public sealed record Request(string? Notes);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("leads/{id:guid}/notes", async (Guid id, Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateLeadNotesCommand(id, request.Notes);
            Result result = await sender.Send(command, ct);
            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsWrite)
        .WithTags(Tags.Leads)
        .WithName("UpdateLeadNotes")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
