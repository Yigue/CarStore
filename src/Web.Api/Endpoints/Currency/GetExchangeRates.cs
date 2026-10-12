using Application.Currency.GetExchangeRates;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Currency;

/// <summary>sfc B0.2.3 / ADR C2: the dealer's exchange-rate history, most recent effective rate first.</summary>
internal sealed class GetExchangeRates : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("dealer/exchange-rates", async (ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<ExchangeRateResponse>> result =
                await sender.Send(new GetExchangeRatesQuery(), cancellationToken);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem);
        })
        .HasPermission("financial:read")
        .WithTags(Tags.Financial)
        .WithName("GetExchangeRates")
        .Produces<IReadOnlyList<ExchangeRateResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
