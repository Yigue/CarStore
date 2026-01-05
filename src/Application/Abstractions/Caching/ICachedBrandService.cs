using Domain.Cars.Atribbutes;
namespace Application.Abstractions.Caching;
public interface ICachedBrandService
{
    Task<Marca?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Marca?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<Marca>> GetAllAsync(CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(CancellationToken cancellationToken = default);
}
