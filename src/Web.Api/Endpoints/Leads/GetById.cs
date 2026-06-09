using Application.Leads.GetById;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Leads;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("leads/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var query = new GetLeadByIdQuery(id);
            Result<LeadDetailResponse> result = await sender.Send(query, ct);
            return result.Match(
                Results.Ok,
                CustomResults.Problem);
        })
        .HasPermission(Permissions.LeadsRead)
        .WithTags(Tags.Leads)
        .WithName("GetLeadById")
        .Produces<LeadDetailResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
