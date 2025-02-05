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

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithTags("Financial");
    }
}
