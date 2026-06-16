using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using SharedKernel;

namespace Application.Modelos.Get;

internal sealed class GetModelosQueryHandler(ICachedModelService modelService)
    : IQueryHandler<GetModelosQuery, List<Modelo>>
{
    public async Task<Result<List<Modelo>>> Handle(GetModelosQuery query, CancellationToken cancellationToken)
    {
        var modelos = await modelService.GetAllAsync(cancellationToken);
        return Result.Success(modelos);
    }
}
