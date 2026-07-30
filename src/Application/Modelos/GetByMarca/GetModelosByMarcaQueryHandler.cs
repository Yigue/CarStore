using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using SharedKernel;

namespace Application.Modelos.GetByMarca;

internal sealed class GetModelosByMarcaQueryHandler(ICachedModelService modelService)
    : IQueryHandler<GetModelosByMarcaQuery, List<ModeloCacheDto>>
{
    public async Task<Result<List<ModeloCacheDto>>> Handle(GetModelosByMarcaQuery query, CancellationToken cancellationToken)
    {
        var modelos = await modelService.GetByBrandIdAsync(query.MarcaId, cancellationToken);
        return Result.Success(modelos);
    }
}
