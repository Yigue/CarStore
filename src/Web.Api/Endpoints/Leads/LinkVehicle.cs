using Application.Leads.LinkVehicle;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class LinkVehicle : IEndpoint
{
    public sealed record Request(Guid VehicleId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("leads/{id:guid}/vehicle", async (Guid id, Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new LinkVehicleToLeadCommand(id, request.VehicleId);
            Result result = await sender.Send(command, ct);
            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsUpdate)
        .WithTags(Tags.Leads)
        .WithName("LinkVehicleToLead")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
