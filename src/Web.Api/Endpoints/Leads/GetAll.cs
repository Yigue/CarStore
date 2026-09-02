using Application.Leads.GetAll;
using Domain.Leads;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("leads", async (LeadStatus? status, bool includeArchived = false, ISender sender = default!, CancellationToken ct = default) =>
        {
            var query = new GetLeadsQuery(status, includeArchived);
            Result<List<LeadResponse>> result = await sender.Send(query, ct);
            return result.Match(
                leads => Results.Ok(leads),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsRead)
        .WithTags(Tags.Leads)
        .WithName("GetAllLeads")
        .Produces<List<LeadResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
