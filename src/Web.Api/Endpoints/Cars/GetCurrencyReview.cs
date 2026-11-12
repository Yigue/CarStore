using Application.Cars.CurrencyReview;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

/// <summary>sfc B0.1.5 / ADR C1: cars whose purchase cost currency differs from their price currency, for manual review.</summary>
internal sealed class GetCurrencyReview : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("dealer/currency-review", async (ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<CurrencyReviewItemResponse>> result =
                await sender.Send(new GetCurrencyReviewQuery(), cancellationToken);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem);
        })
        .HasPermission("financial:read")
        .WithTags(Tags.Cars)
        .WithName("GetCurrencyReview")
        .Produces<IReadOnlyList<CurrencyReviewItemResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
