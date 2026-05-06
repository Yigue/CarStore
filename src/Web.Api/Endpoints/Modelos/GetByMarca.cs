using Application.Modelos.GetByMarca;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Modelos;

public sealed class GetByMarca : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("modelos/marca/{marcaId}", Handler)
            .WithTags(Tags.Modelos)
            .RequireAuthorization();
    }

    private static async Task<IResult> Handler(Guid marcaId, ISender sender, CancellationToken cancellationToken)
    {
        Result<List<Domain.Cars.Attributes.Modelo>> result = await sender.Send(new GetModelosByMarcaQuery(marcaId), cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
    }
}
