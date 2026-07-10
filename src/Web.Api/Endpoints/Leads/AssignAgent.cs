using Application.Leads.AssignAgent;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class AssignAgent : IEndpoint
{
    public sealed record Request(Guid AgentId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("leads/{id:guid}/agent", async (Guid id, Request request, ISender sender, CancellationToken ct) =>
        {
            var command = new AssignAgentToLeadCommand(id, request.AgentId);
            Result result = await sender.Send(command, ct);
            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsWrite)
        .WithTags(Tags.Leads)
        .WithName("AssignAgentToLead")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
