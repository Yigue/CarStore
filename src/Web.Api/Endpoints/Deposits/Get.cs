using Application.Deposits.Get;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Deposits;

/// <summary>sfc B1.3.5: the dealer's deposits, optionally for one car.</summary>
internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("deposits", async ([FromQuery] Guid? carId, ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<DepositResponse>> result = await sender.Send(new GetDepositsQuery(carId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("sales:read")
        .WithTags(Tags.Sales)
        .WithName("GetDeposits")
        .Produces<IReadOnlyList<DepositResponse>>(StatusCodes.Status200OK);
    }
}
