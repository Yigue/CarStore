using Application.Quotes.Send;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

/// <summary>
/// sfc B4.3 / B10 / RN-018: <c>POST quotes/{id}/send</c> emails the quote's proposal PDF (via a
/// presigned download URL — see <c>SendQuoteCommandHandler</c>) to the quote's client and
/// records a lead activity entry when a lead resolves for it.
/// </summary>
internal sealed class Send : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("quotes/{id:guid}/send", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new SendQuoteCommand(id);

            Result<SendQuoteResult> result = await sender.Send(command, cancellationToken);

            return result.Match(
                value => Results.Ok(new { documentId = value.DocumentId, quoteUrl = value.QuoteUrl }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.QuotesUpdate)
        .WithTags(Tags.Quotes)
        .WithName("SendQuote")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
