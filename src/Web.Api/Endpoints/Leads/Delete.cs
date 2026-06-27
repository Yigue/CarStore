using Application.Leads.Archive;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("leads/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var command = new ArchiveLeadCommand(id);
            Result result = await sender.Send(command, ct);
            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsWrite)
        .WithTags(Tags.Leads)
        .WithName("DeleteLead")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
