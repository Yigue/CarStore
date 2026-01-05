using Domain.Financial.Attributes;
namespace Application.Abstractions.Caching;
public interface ICachedCategoryService
{
    Task<TransactionCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TransactionCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<TransactionCategory>> GetAllAsync(CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(CancellationToken cancellationToken = default);
}
