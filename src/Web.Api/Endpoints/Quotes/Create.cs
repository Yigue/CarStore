using Application.Quotes.Create;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Quotes;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public Guid CarId { get; set; }
        public Guid ClientId { get; set; }
        public decimal ProposedPrice { get; set; }
        public DateTime ValidUntil { get; set; }
        public string Comments { get; set; } = string.Empty;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("quotes", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateQuoteCommand(
                request.CarId,
                request.ClientId,
                request.ProposedPrice,
                request.ValidUntil,
                request.Comments);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/quotes/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.QuotesCreate)
        .WithTags(Tags.Quotes)
        .WithName("CreateQuote")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

