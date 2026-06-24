using Domain.Cars.Attributes;

namespace Application.Abstractions.Caching;

public interface ICachedModelService
{
    Task<Modelo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Modelo>> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<List<Modelo>> GetAllAsync(CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(CancellationToken cancellationToken = default);
    Task InvalidateBrandCacheAsync(Guid brandId, CancellationToken cancellationToken = default);
}
