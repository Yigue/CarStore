using Domain.Cars.Attributes;

namespace Application.Abstractions.Caching;

public interface ICachedBrandService
{
    Task<MarcaCacheDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MarcaCacheDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<MarcaCacheDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(CancellationToken cancellationToken = default);
}
