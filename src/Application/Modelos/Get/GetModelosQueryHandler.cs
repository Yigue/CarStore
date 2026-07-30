using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using SharedKernel;

namespace Application.Modelos.Get;

internal sealed class GetModelosQueryHandler(ICachedModelService modelService)
    : IQueryHandler<GetModelosQuery, List<ModeloCacheDto>>
{
    public async Task<Result<List<ModeloCacheDto>>> Handle(GetModelosQuery query, CancellationToken cancellationToken)
    {
        var modelos = await modelService.GetAllAsync(cancellationToken);
        return Result.Success(modelos);
    }
}
