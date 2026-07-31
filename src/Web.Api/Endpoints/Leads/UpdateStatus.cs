using Application.Leads.UpdateStatus;
using Domain.Leads;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class UpdateStatus : IEndpoint
{
    // qa-p1-integridad D2: NewStatus is nullable so an omitted value binds to null instead of
    // silently defaulting to LeadStatus.Nuevo (member 0). UpdateLeadStatusCommandValidator's
    // NotNull rule rejects the omission with a typed 400 before the handler ever sees it.
    public sealed record Request(LeadStatus? NewStatus, string? Notes = null, LeadLossReason? LossReason = null);

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
        .HasPermission(Permissions.LeadsWrite)
        .WithTags(Tags.Leads)
        .WithName("UpdateLeadStatus")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
