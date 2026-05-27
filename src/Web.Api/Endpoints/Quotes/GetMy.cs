using Application.Quotes.Get;
using Application.Quotes.GetMy;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

internal sealed class GetMy : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("quotes/my", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetMyQuotesQuery();

            Result<List<QuoteResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                quotes => Results.Ok(quotes),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Quotes)
        .WithName("GetMyQuotes")
        .Produces<List<QuoteResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
