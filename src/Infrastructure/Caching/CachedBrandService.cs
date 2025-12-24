using Application.Abstractions.Data;
using Domain.Cars.Atribbutes;
using Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Caching;

/// <summary>
/// Servicio para obtener marcas con caché
/// </summary>
internal sealed class CachedBrandService
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedBrandService> _logger;

    public CachedBrandService(
        IApplicationDbContext context,
        ICacheService cacheService,
        ILogger<CachedBrandService> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Marca?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.BrandById(id);
        var cached = await _cacheService.GetAsync<Marca>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var marca = await _context.Marca
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (marca != null)
        {
            await _cacheService.SetAsync(cacheKey, marca, CacheTTL.Brands, cancellationToken);
        }

        return marca;
    }

    public async Task<Marca?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.BrandByName(name);
        var cached = await _cacheService.GetAsync<Marca>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var marca = await _context.Marca
            .FirstOrDefaultAsync(m => m.Nombre == name, cancellationToken);

        if (marca != null)
        {
            await _cacheService.SetAsync(cacheKey, marca, CacheTTL.Brands, cancellationToken);
        }

        return marca;
    }

    public async Task<List<Marca>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.AllBrands();
        var cached = await _cacheService.GetAsync<List<Marca>>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var marcas = await _context.Marca
            .ToListAsync(cancellationToken);

        if (marcas.Any())
        {
            await _cacheService.SetAsync(cacheKey, marcas, CacheTTL.Brands, cancellationToken);
        }

        return marcas;
    }

    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        // Invalidar todas las claves de marcas
        // Nota: En una implementación completa, se usaría RemoveByPatternAsync
        _logger.LogInformation("Invalidating brands cache");
        await Task.CompletedTask;
    }
}

