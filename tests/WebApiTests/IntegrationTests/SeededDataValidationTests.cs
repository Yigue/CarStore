using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiTests.IntegrationTests;

/// <summary>
/// Tests para validar que los datos seedeados estén disponibles
/// </summary>
public class SeededDataValidationTests
{
    [Fact]
    public async Task SeededData_ShouldContainFourBrands()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var brands = await context.Marca.ToListAsync();

        brands.Should().HaveCount(4);
        brands.Should().Contain(b => b.Nombre == "Toyota");
        brands.Should().Contain(b => b.Nombre == "Ford");
        brands.Should().Contain(b => b.Nombre == "Chevrolet");
        brands.Should().Contain(b => b.Nombre == "Volkswagen");
    }

    [Fact]
    public async Task SeededData_ShouldContainSixteenModels()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var models = await context.Modelo.ToListAsync();

        models.Should().HaveCount(16);
        
        // Verificar modelos de Toyota
        var toyota = await context.Marca.FirstAsync(m => m.Nombre == "Toyota");
        var toyotaModels = await context.Modelo
            .Where(m => m.MarcaId == toyota.Id)
            .ToListAsync();
        toyotaModels.Should().HaveCount(4);
        toyotaModels.Should().Contain(m => m.Nombre == "Corolla");
        toyotaModels.Should().Contain(m => m.Nombre == "Camry");
        toyotaModels.Should().Contain(m => m.Nombre == "RAV4");
        toyotaModels.Should().Contain(m => m.Nombre == "Hilux");
    }

    [Fact]
    public async Task SeededData_ShouldContainSevenTransactionCategories()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var categories = await context.TransactionCategories.ToListAsync();

        categories.Should().HaveCount(7);
        categories.Should().Contain(c => c.Name == "Venta de Auto");
        categories.Should().Contain(c => c.Name == "Servicio Técnico");
        categories.Should().Contain(c => c.Name == "Garantía");
        categories.Should().Contain(c => c.Name == "Compra de Auto");
        categories.Should().Contain(c => c.Name == "Gastos Operativos");
        categories.Should().Contain(c => c.Name == "Mantenimiento");
        categories.Should().Contain(c => c.Name == "Publicidad");
    }

    [Fact]
    public async Task SeededData_ShouldContainAdminUser()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var admin = await context.Users
            .FirstOrDefaultAsync(u => u.Email == "admin@carstore.com");

        admin.Should().NotBeNull();
        admin!.Email.Should().Be("admin@carstore.com");
        admin.FirstName.Should().Be("Admin");
        admin.LastName.Should().Be("User");
    }
}

