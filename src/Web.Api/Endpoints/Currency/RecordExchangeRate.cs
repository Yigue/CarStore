using Application.Currency.RecordExchangeRate;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Currency;

/// <summary>sfc B0.2.3 / ADR C2: the dealer loads a manual exchange rate (append-only).</summary>
internal sealed class RecordExchangeRate : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("dealer/exchange-rates", async (
            RecordExchangeRateCommand command,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Ok(new { id }),
                CustomResults.Problem);
        })
        .HasPermission("financial:create")
        .WithTags(Tags.Financial)
        .WithName("RecordExchangeRate")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
