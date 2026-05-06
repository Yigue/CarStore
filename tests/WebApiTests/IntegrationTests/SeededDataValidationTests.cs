namespace WebApiTests.IntegrationTests;
using Application.Abstractions.Data;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Xunit;
using Infrastructure.Database;



/// <summary>
/// Valida que los datos iniciales (Seed Data) se hayan cargado correctamente.
/// </summary>
public class SeededDataValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SeededDataValidationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeededData_ShouldContainEightBrands()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var brands = await context.Marca.IgnoreQueryFilters().ToListAsync();

        brands.Should().HaveCount(8);
        brands.Should().Contain(b => b.Nombre == "Toyota");
        brands.Should().Contain(b => b.Nombre == "Ford");
        brands.Should().Contain(b => b.Nombre == "Chevrolet");
        brands.Should().Contain(b => b.Nombre == "Volkswagen");
    }

    [Fact]
    public async Task SeededData_ShouldContainSeventeenModels()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var models = await context.Modelo.IgnoreQueryFilters().ToListAsync();

        models.Should().HaveCount(17);
        models.Should().Contain(m => m.Nombre == "Corolla");
        models.Should().Contain(m => m.Nombre == "Fiesta");
        models.Should().Contain(m => m.Nombre == "Cruze");
        models.Should().Contain(m => m.Nombre == "Gol");
    }
}
 