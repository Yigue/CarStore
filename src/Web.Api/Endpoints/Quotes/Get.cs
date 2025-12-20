using Application.Quotes.Get;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("quotes", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetQuotesQuery();

            Result<List<QuoteResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                quotes => Results.Ok(quotes),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.QuotesRead)
        .WithTags(Tags.Quotes)
        .WithName("GetQuotes")
        .Produces<List<QuoteResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

