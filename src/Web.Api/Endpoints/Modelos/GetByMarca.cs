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
        app.MapGet("modelos/marca/{marcaId:guid}", Handler)
            .WithTags(Tags.Modelos)
            .AllowAnonymous();
    }

    private static async Task<IResult> Handler(Guid marcaId, ISender sender, CancellationToken cancellationToken)
    {
        Result<List<Application.Abstractions.Caching.ModeloCacheDto>> result = await sender.Send(new GetModelosByMarcaQuery(marcaId), cancellationToken);

        return result.Match(
            modelos => Results.Ok(modelos.Select(m => new { id = m.Id, nombre = m.Nombre, marcaId = m.MarcaId })),
            CustomResults.Problem);
    }
}
