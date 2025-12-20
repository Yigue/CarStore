using Application.Financial.GetAll;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Financial;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("financial", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetAllFinancialsQuery();

            Result<IReadOnlyList<FinancialResponses>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                financials => Results.Ok(financials),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.FinancialRead)
        .WithTags(Tags.Financial)
        .WithName("GetAllFinancials")
        .Produces<IReadOnlyList<FinancialResponses>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
