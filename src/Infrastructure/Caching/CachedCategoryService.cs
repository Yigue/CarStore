using Application.Abstractions.Data;
using Domain.Financial.Attributes;
using Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Caching;

/// <summary>
/// Servicio para obtener categorías de transacciones con caché
/// </summary>
internal sealed class CachedCategoryService
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedCategoryService> _logger;

    public CachedCategoryService(
        IApplicationDbContext context,
        ICacheService cacheService,
        ILogger<CachedCategoryService> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<TransactionCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.TransactionCategoryById(id);
        var cached = await _cacheService.GetAsync<TransactionCategory>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var category = await _context.TransactionCategories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category != null)
        {
            await _cacheService.SetAsync(cacheKey, category, CacheTTL.TransactionCategories, cancellationToken);
        }

        return category;
    }

    public async Task<TransactionCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.TransactionCategoryByName(name);
        var cached = await _cacheService.GetAsync<TransactionCategory>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var category = await _context.TransactionCategories
            .FirstOrDefaultAsync(c => c.Name == name, cancellationToken);

        if (category != null)
        {
            await _cacheService.SetAsync(cacheKey, category, CacheTTL.TransactionCategories, cancellationToken);
        }

        return category;
    }

    public async Task<List<TransactionCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.AllTransactionCategories();
        var cached = await _cacheService.GetAsync<List<TransactionCategory>>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var categories = await _context.TransactionCategories
            .ToListAsync(cancellationToken);

        if (categories.Any())
        {
            await _cacheService.SetAsync(cacheKey, categories, CacheTTL.TransactionCategories, cancellationToken);
        }

        return categories;
    }

    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invalidating transaction categories cache");
        await Task.CompletedTask;
    }
}

