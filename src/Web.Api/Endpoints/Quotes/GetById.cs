using Application.Quotes.GetById;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("quotes/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetQuoteByIdQuery(id);

            Result<Application.Quotes.Get.QuoteResponse> result = await sender.Send(query, cancellationToken);

            return result.Match(
                quote => Results.Ok(quote),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.QuotesRead)
        .WithTags(Tags.Quotes)
        .WithName("GetQuoteById")
        .Produces<Application.Quotes.Get.QuoteResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

