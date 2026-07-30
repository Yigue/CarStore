using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using SharedKernel;

namespace Application.Marcas.Get;

internal sealed class GetMarcasQueryHandler(ICachedBrandService brandService)
    : IQueryHandler<GetMarcasQuery, List<MarcaCacheDto>>
{
    public async Task<Result<List<MarcaCacheDto>>> Handle(GetMarcasQuery query, CancellationToken cancellationToken)
    {
        var marcas = await brandService.GetAllAsync(cancellationToken);
        return Result.Success(marcas);
    }
}
