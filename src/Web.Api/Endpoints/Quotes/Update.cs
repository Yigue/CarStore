using Application.Quotes.Update;
using Domain.Quotes.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

internal sealed class Update : IEndpoint
{
    public sealed class Request
    {
        public decimal ProposedPrice { get; set; }
        public DateTime ValidUntil { get; set; }
        public string Comments { get; set; } = string.Empty;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("quotes/{id:guid}", async (
            Guid id,
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateQuoteCommand(
                id,
                request.ProposedPrice,
                request.ValidUntil,
                request.Comments);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                quoteId => Results.Ok(new { id = quoteId }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.QuotesUpdate)
        .WithTags(Tags.Quotes)
        .WithName("UpdateQuote")
        .Produces<Guid>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

