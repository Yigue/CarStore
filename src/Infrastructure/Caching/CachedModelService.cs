using Application.Abstractions.Data;
using Domain.Cars.Atribbutes;
using Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Caching;

/// <summary>
/// Servicio para obtener modelos con caché
/// </summary>
internal sealed class CachedModelService
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

    public async Task<Modelo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.ModelById(id);
        var cached = await _cacheService.GetAsync<Modelo>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var modelo = await _context.Modelo
            .Include(m => m.Marca)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (modelo != null)
        {
            await _cacheService.SetAsync(cacheKey, modelo, CacheTTL.Models, cancellationToken);
        }

        return modelo;
    }

    public async Task<List<Modelo>> GetByBrandIdAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.ModelsByBrand(brandId);
        var cached = await _cacheService.GetAsync<List<Modelo>>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var modelos = await _context.Modelo
            .Include(m => m.Marca)
            .Where(m => m.MarcaId == brandId)
            .ToListAsync(cancellationToken);

        if (modelos.Any())
        {
            await _cacheService.SetAsync(cacheKey, modelos, CacheTTL.Models, cancellationToken);
        }

        return modelos;
    }

    public async Task<List<Modelo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.AllModels();
        var cached = await _cacheService.GetAsync<List<Modelo>>(cacheKey, cancellationToken);
        
        if (cached != null)
        {
            return cached;
        }

        var modelos = await _context.Modelo
            .Include(m => m.Marca)
            .ToListAsync(cancellationToken);

        if (modelos.Any())
        {
            await _cacheService.SetAsync(cacheKey, modelos, CacheTTL.Models, cancellationToken);
        }

        return modelos;
    }

    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invalidating models cache");
        await Task.CompletedTask;
    }
}

