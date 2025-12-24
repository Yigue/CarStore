using System.Net.Http.Headers;
using Application.Abstractions.Data;
using Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiTests;

/// <summary>
/// Helpers para tests de integración
/// </summary>
public static class IntegrationTestHelpers
{
    /// <summary>
    /// Obtiene un token de autenticación para el usuario admin seedeado
    /// </summary>
    public static async Task<string> GetAdminTokenAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var loginRequest = new
        {
            Email = "admin@carstore.com",
            Password = "Admin123!"
        };

        var loginResponse = await client.PostAsJsonAsync("/users/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        
        var token = await loginResponse.Content.ReadAsStringAsync();
        return token.Trim('"');
    }

    /// <summary>
    /// Configura el cliente HTTP con el token de autenticación
    /// </summary>
    public static void SetAuthToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Obtiene una marca seedeada por nombre
    /// </summary>
    public static async Task<Domain.Cars.Atribbutes.Marca> GetSeededBrandAsync(
        CustomWebApplicationFactory factory,
        string brandName)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        return await context.Marca
            .FirstAsync(m => m.Nombre == brandName);
    }

    /// <summary>
    /// Obtiene un modelo seedeado por nombre
    /// </summary>
    public static async Task<Domain.Cars.Atribbutes.Modelo> GetSeededModelAsync(
        CustomWebApplicationFactory factory,
        string modelName)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        return await context.Modelo
            .FirstAsync(m => m.Nombre == modelName);
    }

    /// <summary>
    /// Obtiene una categoría de transacción seedeada por nombre
    /// </summary>
    public static async Task<Domain.Financial.Attributes.TransactionCategory> GetSeededCategoryAsync(
        CustomWebApplicationFactory factory,
        string categoryName)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        return await context.TransactionCategories
            .FirstAsync(c => c.Name == categoryName);
    }
}

