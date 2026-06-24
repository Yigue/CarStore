using Application.Leads.UpdateStatus;
using Domain.Leads;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class UpdateStatus : IEndpoint
{
    public sealed record Request(LeadStatus NewStatus, string? Notes = null, LeadLossReason? LossReason = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("leads/{id:guid}/status", async (Guid id, Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new UpdateLeadStatusCommand(id, request.NewStatus, request.Notes, request.LossReason);
            Result result = await sender.Send(command, ct);
            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsUpdate)
        .WithTags(Tags.Leads)
        .WithName("UpdateLeadStatus")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
