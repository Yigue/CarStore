using Domain.Cars.Attributes;

namespace Application.Abstractions.Caching;

public interface ICachedModelService
{
    Task<ModeloCacheDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<ModeloCacheDto>> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<List<ModeloCacheDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(CancellationToken cancellationToken = default);
    Task InvalidateBrandCacheAsync(Guid brandId, CancellationToken cancellationToken = default);
}
