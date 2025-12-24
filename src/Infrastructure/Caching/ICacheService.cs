namespace Infrastructure.Caching;

/// <summary>
/// Servicio de caché distribuido usando Redis
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Obtiene un valor del caché
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Guarda un valor en el caché con TTL
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Elimina un valor del caché
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina todas las claves que coincidan con el patrón
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si una clave existe en el caché
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}

