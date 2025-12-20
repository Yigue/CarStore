using Application.Quotes.Reject;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

internal sealed class Reject : IEndpoint
{
    public sealed class Request
    {
        public string Reason { get; set; } = string.Empty;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("quotes/{id:guid}/reject", async (
            Guid id,
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new RejectQuoteCommand(id, request.Reason);

            Result result = await sender.Send(command, cancellationToken);

            return result.Match(
                () => Results.Ok(new { message = "Quote rejected successfully", id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.QuotesReject)
        .WithTags(Tags.Quotes)
        .WithName("RejectQuote")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

