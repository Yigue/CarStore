using Application.Financing.Commands.CalculateFinancing;
using Application.Financing.Dtos;
using MediatR;

namespace Web.Api.Endpoints.Financing;

public sealed class CalculateFinancingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/financing/calculate", async (
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