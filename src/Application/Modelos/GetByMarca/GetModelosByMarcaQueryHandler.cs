using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Domain.Cars.Atribbutes;
using SharedKernel;

namespace Application.Modelos.GetByMarca;

internal sealed class GetModelosByMarcaQueryHandler(ICachedModelService modelService)
    : IQueryHandler<GetModelosByMarcaQuery, List<Modelo>>
{
    public async Task<Result<List<Modelo>>> Handle(GetModelosByMarcaQuery query, CancellationToken cancellationToken)
    {
        var modelos = await modelService.GetByBrandIdAsync(query.MarcaId, cancellationToken);
        return Result.Success(modelos);
    }
}
