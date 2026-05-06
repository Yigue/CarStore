using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using SharedKernel;

namespace Application.Marcas.Get;

internal sealed class GetMarcasQueryHandler(ICachedBrandService brandService)
    : IQueryHandler<GetMarcasQuery, List<Marca>>
{
    public async Task<Result<List<Marca>>> Handle(GetMarcasQuery query, CancellationToken cancellationToken)
    {
        var marcas = await brandService.GetAllAsync(cancellationToken);
        return Result.Success(marcas);
    }
}
