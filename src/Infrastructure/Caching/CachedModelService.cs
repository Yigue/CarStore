using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Caching;

/// <summary>
/// Servicio para obtener modelos con caché
/// </summary>
internal sealed class CachedModelService : ICachedModelService
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedModelService> _logger;

    public CachedModelService(
        IApplicationDbContext context,
        ICacheService cacheService,
        ILogger<CachedModelService> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<ModeloCacheDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.ModelById(id);
        var cached = await _cacheService.GetAsync<ModeloCacheDto>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var modelo = await _context.Modelo
            .Include(m => m.Marca)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (modelo != null)
        {
            var dto = new ModeloCacheDto { Id = modelo.Id, Nombre = modelo.Nombre, MarcaId = modelo.MarcaId };
            await _cacheService.SetAsync(cacheKey, dto, CacheTTL.Models, cancellationToken);
            return dto;
        }

        return null;
    }

    public async Task<List<ModeloCacheDto>> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.ModelsByBrand(brandId);
        var cached = await _cacheService.GetAsync<List<ModeloCacheDto>>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var modelos = await _context.Modelo
            .Include(m => m.Marca)
            .Where(m => m.MarcaId == brandId)
            .ToListAsync(cancellationToken);

        var dtos = modelos.Select(m => new ModeloCacheDto { Id = m.Id, Nombre = m.Nombre, MarcaId = m.MarcaId }).ToList();

        if (dtos.Any())
        {
            await _cacheService.SetAsync(cacheKey, dtos, CacheTTL.Models, cancellationToken);
        }

        return dtos;
    }

    public async Task<List<ModeloCacheDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.AllModels();
        var cached = await _cacheService.GetAsync<List<ModeloCacheDto>>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var modelos = await _context.Modelo
            .Include(m => m.Marca)
            .ToListAsync(cancellationToken);

        var dtos = modelos.Select(m => new ModeloCacheDto { Id = m.Id, Nombre = m.Nombre, MarcaId = m.MarcaId }).ToList();

        if (dtos.Any())
        {
            await _cacheService.SetAsync(cacheKey, dtos, CacheTTL.Models, cancellationToken);
        }

        return dtos;
    }

    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invalidating all models cache");
        await _cacheService.RemoveAsync(CacheKeys.AllModels(), cancellationToken);
    }

    public async Task InvalidateBrandCacheAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invalidating models cache for brand {BrandId}", brandId);
        await _cacheService.RemoveAsync(CacheKeys.ModelsByBrand(brandId), cancellationToken);
        await _cacheService.RemoveAsync(CacheKeys.AllModels(), cancellationToken);
    }
}

