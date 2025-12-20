using Application.Quotes.GetExpired;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

internal sealed class GetExpired : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("quotes/expired", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetExpiredQuotesQuery();

            Result<List<Application.Quotes.Get.QuoteResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                quotes => Results.Ok(quotes),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.QuotesRead)
        .WithTags(Tags.Quotes)
        .WithName("GetExpiredQuotes")
        .Produces<List<Application.Quotes.Get.QuoteResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

