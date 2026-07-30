using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Caching;

/// <summary>
/// Servicio para obtener marcas con caché
/// </summary>
internal sealed class CachedBrandService : ICachedBrandService
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

    public async Task<MarcaCacheDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.BrandById(id);
        var cached = await _cacheService.GetAsync<MarcaCacheDto>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var marca = await _context.Marca
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (marca != null)
        {
            var dto = new MarcaCacheDto { Id = marca.Id, Nombre = marca.Nombre };
            await _cacheService.SetAsync(cacheKey, dto, CacheTTL.Brands, cancellationToken);
            return dto;
        }

        return null;
    }

    public async Task<MarcaCacheDto?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.BrandByName(name);
        var cached = await _cacheService.GetAsync<MarcaCacheDto>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var marca = await _context.Marca
            .FirstOrDefaultAsync(m => m.Nombre == name, cancellationToken);

        if (marca != null)
        {
            var dto = new MarcaCacheDto { Id = marca.Id, Nombre = marca.Nombre };
            await _cacheService.SetAsync(cacheKey, dto, CacheTTL.Brands, cancellationToken);
            return dto;
        }

        return null;
    }

    public async Task<List<MarcaCacheDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.AllBrands();
        var cached = await _cacheService.GetAsync<List<MarcaCacheDto>>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var marcas = await _context.Marca
            .ToListAsync(cancellationToken);

        var dtos = marcas.Select(m => new MarcaCacheDto { Id = m.Id, Nombre = m.Nombre }).ToList();

        if (dtos.Any())
        {
            await _cacheService.SetAsync(cacheKey, dtos, CacheTTL.Brands, cancellationToken);
        }

        return dtos;
    }

    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invalidating brands cache");
        await _cacheService.RemoveAsync(CacheKeys.AllBrands(), cancellationToken);
        _logger.LogWarning("Individual brand cache keys (by id/name) are not invalidated. Consider implementing RemoveByPatternAsync for '{Pattern}'", CacheKeys.BrandsPattern());
    }
}

