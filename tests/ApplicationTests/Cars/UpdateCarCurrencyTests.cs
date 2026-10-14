using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Caching;
using Application.Cars.Update;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Cars;

/// <summary>sfc B0.2.3 (phase 1): UpdateCar keeps the car's currency unless an explicit one is sent, and rejects unknown ones.</summary>
public class UpdateCarCurrencyTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DbContextOptions<TestApplicationDbContext> Options(string db) =>
        new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(db).Options;

    private static async Task<(Guid CarId, Guid MarcaId, Guid ModeloId, string Db)> SeedUsdCarAsync()
    {
        string db = Guid.NewGuid().ToString();
        await using var seed = new TestApplicationDbContext(Options(db), DealerId);
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        seed.Marca.Add(marca);
        seed.Modelo.Add(modelo);
        var car = new Car(DealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 2000, 0, 2020, "CUR001", "seed", 25000m, DateTime.UtcNow,
            priceCurrency: CurrencyCode.Usd);
        seed.Cars.Add(car);
        await seed.SaveChangesAsync();
        return (car.Id, marca.Id, modelo.Id, db);
    }

    private static UpdateCarCommandHandler Handler(TestApplicationDbContext ctx, Guid marcaId, Guid modeloId, Marca marca, Modelo modelo)
    {
        var brands = new Mock<ICachedBrandService>();
        brands.Setup(s => s.GetByIdAsync(marcaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarcaCacheDto { Id = marca.Id, Nombre = marca.Nombre });
        var models = new Mock<ICachedModelService>();
        models.Setup(s => s.GetByIdAsync(modeloId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModeloCacheDto { Id = modelo.Id, Nombre = modelo.Nombre, MarcaId = marcaId });
        return new UpdateCarCommandHandler(ctx, new FakeDateTimeProvider { UtcNow = new DateTime(2026, 10, 1) },
            brands.Object, models.Object);
    }

    private static UpdateCarCommand Command(Guid carId, Guid marcaId, Guid modeloId, decimal price, string? currency) =>
        new(carId, marcaId, modeloId, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 2000, 0, 2020, "CUR001", "seed", price, Currency: currency);

    [Fact]
    public async Task Handle_Should_KeepUsd_WhenThePriceChangesAndNoCurrencyIsSent()
    {
        var (carId, marcaId, modeloId, db) = await SeedUsdCarAsync();
        await using var ctx = new TestApplicationDbContext(Options(db), DealerId);
        var marca = await ctx.Marca.SingleAsync(m => m.Id == marcaId);
        var modelo = await ctx.Modelo.SingleAsync(m => m.Id == modeloId);

        var result = await Handler(ctx, marcaId, modeloId, marca, modelo)
            .Handle(Command(carId, marcaId, modeloId, 26000m, currency: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var car = await ctx.Cars.SingleAsync(c => c.Id == carId);
        car.Price.Amount.Should().Be(26000m);
        car.Price.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Handle_Should_ChangeToArs_WhenAnExplicitCurrencyIsSent()
    {
        var (carId, marcaId, modeloId, db) = await SeedUsdCarAsync();
        await using var ctx = new TestApplicationDbContext(Options(db), DealerId);
        var marca = await ctx.Marca.SingleAsync(m => m.Id == marcaId);
        var modelo = await ctx.Modelo.SingleAsync(m => m.Id == modeloId);

        var result = await Handler(ctx, marcaId, modeloId, marca, modelo)
            .Handle(Command(carId, marcaId, modeloId, 25000m, currency: "ARS"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ctx.Cars.SingleAsync(c => c.Id == carId)).Price.Currency.Should().Be("ARS");
    }

    [Fact]
    public async Task Handle_Should_RejectAnUnsupportedCurrency()
    {
        var (carId, marcaId, modeloId, db) = await SeedUsdCarAsync();
        await using var ctx = new TestApplicationDbContext(Options(db), DealerId);
        var marca = await ctx.Marca.SingleAsync(m => m.Id == marcaId);
        var modelo = await ctx.Modelo.SingleAsync(m => m.Id == modeloId);

        var result = await Handler(ctx, marcaId, modeloId, marca, modelo)
            .Handle(Command(carId, marcaId, modeloId, 25000m, currency: "EUR"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Unsupported");
    }
}
