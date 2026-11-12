using Application.Cars.Liens;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

/// <summary>sfc B7.2 / RN-037: liens (prendas) over a vehicle.</summary>
internal sealed class VehicleLiens : IEndpoint
{
    public sealed record Request(string Creditor, decimal Amount, string Currency, string? RegistrationNumber);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/{id:guid}/liens", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<VehicleLienResponse>> result = await sender.Send(new GetVehicleLiensQuery(id), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("cars:read")
        .WithTags(Tags.Cars)
        .WithName("GetVehicleLiens");

        app.MapPost("cars/{id:guid}/liens", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new RegisterVehicleLienCommand(
                CarId: id,
                Creditor: request.Creditor,
                Amount: request.Amount,
                Currency: request.Currency,
                RegistrationNumber: request.RegistrationNumber);
            Result<Guid> result = await sender.Send(command, cancellationToken);
            return result.Match(lienId => Results.Created($"/cars/{id}/liens/{lienId}", new { id = lienId }), CustomResults.Problem);
        })
        .HasPermission("cars:update")
        .WithTags(Tags.Cars)
        .WithName("RegisterVehicleLien");

        app.MapPost("liens/{id:guid}/liquidate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new LiquidateVehicleLienCommand(id), cancellationToken);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission("cars:update")
        .WithTags(Tags.Cars)
        .WithName("LiquidateVehicleLien");
    }
}
