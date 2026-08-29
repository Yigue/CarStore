using Application.Leads.GetActivity;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

/// <summary>
/// The lead's history. Written only by domain event handlers, so it records what happened rather
/// than what someone typed into a notes box.
/// </summary>
internal sealed class GetActivity : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("leads/{id:guid}/activity", async (
            Guid id,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetLeadActivityQuery(id, page ?? 1, pageSize ?? 50);
            Result<LeadActivityResponse> result = await sender.Send(query, ct);
            return result.Match(
                Results.Ok,
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsRead)
        .WithTags(Tags.Leads)
        .WithName("GetLeadActivity")
        .Produces<LeadActivityResponse>(StatusCodes.Status200OK)
        // No ProducesProblem(500): ProducesProblemBudgetTests freezes that count as a one-way
        // ratchet downward, and a 500 is never a designed part of a contract.
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
