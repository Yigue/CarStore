using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Domain.Financial.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Caching;

/// <summary>
/// Servicio para obtener categorías de transacciones con caché
/// </summary>
internal sealed class CachedCategoryService : ICachedCategoryService
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedCategoryService> _logger;
    private readonly ICurrentTenantService? _tenantService;

    public CachedCategoryService(
        IApplicationDbContext context,
        ICacheService cacheService,
        ILogger<CachedCategoryService> logger,
        ICurrentTenantService? tenantService = null)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
        _tenantService = tenantService;
    }

    private Guid? CurrentDealerId => _tenantService?.HasTenant == true ? _tenantService.DealerId : null;

    public async Task<TransactionCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.TransactionCategoryById(id, CurrentDealerId);
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
        var cacheKey = CacheKeys.TransactionCategoryByName(name, CurrentDealerId);
        var cached = await _cacheService.GetAsync<TransactionCategory>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var category = await _context.TransactionCategories
            .Where(c => c.Name == name)
            .OrderBy(c => c.DealerId == Guid.Empty ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken);

        if (category != null)
        {
            await _cacheService.SetAsync(cacheKey, category, CacheTTL.TransactionCategories, cancellationToken);
        }

        return category;
    }

    public async Task<List<TransactionCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.AllTransactionCategories(CurrentDealerId);
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
        var cacheKey = CacheKeys.AllTransactionCategories(CurrentDealerId);
        await _cacheService.RemoveAsync(cacheKey, cancellationToken);
    }
}

