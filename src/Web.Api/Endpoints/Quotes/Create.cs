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
        public Guid? ClientId { get; set; }
        public Guid? LeadId { get; set; }
        public decimal ProposedPrice { get; set; }
        public Domain.Quotes.Attributes.PaymentMethod PaymentMethod { get; set; }
        public DateTime ValidUntil { get; set; }
        public string Comments { get; set; } = string.Empty;
        // sfc B0.2.3 phase 1: optional; ARS when omitted. The exchange-rate requirement for other currencies is next.
        public string? Currency { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("quotes", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateQuoteCommand(
                CarId: request.CarId,
                ClientId: request.ClientId,
                LeadId: request.LeadId,
                ProposedPrice: request.ProposedPrice,
                PaymentMethod: request.PaymentMethod,
                ValidUntil: request.ValidUntil,
                Comments: request.Comments,
                Currency: request.Currency);

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

