using Application.Quotes.Accept;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

internal sealed class Accept : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("quotes/{id:guid}/accept", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new AcceptQuoteCommand(id);

            Result result = await sender.Send(command, cancellationToken);

            return result.Match(
                () => Results.Ok(new { message = "Quote accepted successfully", id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.QuotesAccept)
        .WithTags(Tags.Quotes)
        .WithName("AcceptQuote")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

