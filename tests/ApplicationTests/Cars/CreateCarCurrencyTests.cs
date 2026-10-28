using Application.Abstractions.Caching;
using Application.Billing.Entitlements;
using Application.Cars.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Application.Abstractions.Tenancy;

namespace Application.UnitTests.Cars;

/// <summary>
/// sfc B0.2.3 (phase 1): CreateCar accepts an explicit price currency. Without it the car keeps ARS, the legacy behaviour,
/// until the currency becomes mandatory in phase 2.
/// </summary>
public class CreateCarCurrencyTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(CreateCarCommandHandler Handler, Marca Marca, Modelo Modelo)> ArrangeAsync(TestApplicationDbContext context)
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();

        var brands = new Mock<ICachedBrandService>();
        brands.Setup(s => s.GetByIdAsync(marca.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarcaCacheDto { Id = marca.Id, Nombre = marca.Nombre });
        var models = new Mock<ICachedModelService>();
        models.Setup(s => s.GetByIdAsync(modelo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModeloCacheDto { Id = modelo.Id, Nombre = modelo.Nombre, MarcaId = modelo.MarcaId });
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(s => s.EnsureCanAddAsync(It.IsAny<Guid>(), It.IsAny<PlanResource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new CreateCarCommandHandler(
            context, context, new FakeDateTimeProvider { UtcNow = new DateTime(2026, 10, 1) }, brands.Object, models.Object, tenant.Object, entitlementService.Object);
        return (handler, marca, modelo);
    }

    private static CreateCarCommand Command(Guid marcaId, Guid modeloId, string? currency, string patente = "CUR123") =>
        new(marcaId, modeloId, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 2000, 0, 2026, patente, "desc", 20000m, Currency: currency);

    [Fact]
    public async Task Handle_Should_StorePriceInTheRequestedCurrency()
    {
        using var context = CreateContext();
        var (handler, marca, modelo) = await ArrangeAsync(context);

        var result = await handler.Handle(Command(marca.Id, modelo.Id, "usd"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Cars.SingleAsync(c => c.Id == result.Value)).Price.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Handle_Should_KeepArs_WhenNoCurrencyIsSent()
    {
        using var context = CreateContext();
        var (handler, marca, modelo) = await ArrangeAsync(context);

        var result = await handler.Handle(Command(marca.Id, modelo.Id, currency: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Cars.SingleAsync(c => c.Id == result.Value)).Price.Currency.Should().Be("ARS");
    }

    [Fact]
    public async Task Handle_Should_RejectAnUnsupportedCurrency_WithoutCreatingTheCar()
    {
        using var context = CreateContext();
        var (handler, marca, modelo) = await ArrangeAsync(context);

        var result = await handler.Handle(Command(marca.Id, modelo.Id, "EUR"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Currency.Unsupported");
        (await context.Cars.CountAsync()).Should().Be(0);
    }
}
