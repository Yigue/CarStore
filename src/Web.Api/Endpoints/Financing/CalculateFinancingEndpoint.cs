using Application.Financing.Commands.CalculateFinancing;
using Application.Financing.Dtos;
using MediatR;

namespace Web.Api.Endpoints.Financing;

public sealed class CalculateFinancingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Relative path: IEndpoints are mapped onto the versioned group
        // (api/v{version}), so an absolute /api/v1 path would double-prefix.
        app.MapPost("financing/calculate", async (
            FinancingCalculationRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CalculateFinancingCommand(
                request.VehiclePrice,
                request.Installments,
                request.TnaOverride);

            var result = await sender.Send(command, ct);
            return result.Match(
                data => Results.Ok(data),
                error => Results.BadRequest(error));
        })
        .WithTags("Financing")
        .WithName("CalculateFinancing")
        .Produces<FinancingCalculationResponse>()
        .AllowAnonymous();
    }
}