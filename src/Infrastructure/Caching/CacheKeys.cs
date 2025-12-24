namespace Infrastructure.Caching;

/// <summary>
/// Claves de caché utilizadas en la aplicación
/// </summary>
public static class CacheKeys
{
    // Permisos
    public static string UserPermissions(Guid userId) => $"permissions:user:{userId}";
    
    // Marcas
    public static string AllBrands() => "brands:all";
    public static string BrandById(Guid id) => $"brands:id:{id}";
    public static string BrandByName(string name) => $"brands:name:{name.ToLowerInvariant()}";
    
    // Modelos
    public static string AllModels() => "models:all";
    public static string ModelsByBrand(Guid brandId) => $"models:brand:{brandId}";
    public static string ModelById(Guid id) => $"models:id:{id}";
    
    // Categorías de transacciones
    public static string AllTransactionCategories() => "transaction-categories:all";
    public static string TransactionCategoryById(Guid id) => $"transaction-categories:id:{id}";
    public static string TransactionCategoryByName(string name) => $"transaction-categories:name:{name.ToLowerInvariant()}";
    
    // Patrones para invalidación
    public static string BrandsPattern() => "brands:*";
    public static string ModelsPattern() => "models:*";
    public static string TransactionCategoriesPattern() => "transaction-categories:*";
}

/// <summary>
/// TTL (Time To Live) para diferentes tipos de datos en caché
/// </summary>
public static class CacheTTL
{
    // Permisos: 30 minutos (pueden cambiar con frecuencia)
    public static TimeSpan Permissions => TimeSpan.FromMinutes(30);
    
    // Marcas y Modelos: 1 hora (cambian raramente)
    public static TimeSpan Brands => TimeSpan.FromHours(1);
    public static TimeSpan Models => TimeSpan.FromHours(1);
    
    // Categorías: 2 horas (cambian muy raramente)
    public static TimeSpan TransactionCategories => TimeSpan.FromHours(2);
}

