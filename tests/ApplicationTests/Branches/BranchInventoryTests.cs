using Application.Abstractions.Caching;
using Application.Abstractions.Tenancy;
using Application.Branches;
using Application.Cars.Create;
using Application.Queries.Cars.GetInventoryStats;
using Domain.Branches;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Branches;

/// <summary>sfc C2.1 / RN-048 scenarios: a vehicle is assigned to one of the dealer's branches; stock is grouped by branch.</summary>
public class BranchInventoryTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ICurrentTenantService Tenant(Guid dealerId)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        return tenant.Object;
    }

    private static async Task<(TestApplicationDbContext Context, CreateCarCommandHandler Cars, Marca Marca, Modelo Modelo)> SeedAsync()
    {
        var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();
        var brands = new Mock<ICachedBrandService>();
        brands.Setup(s => s.GetByIdAsync(marca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new MarcaCacheDto { Id = marca.Id, Nombre = marca.Nombre });
        var models = new Mock<ICachedModelService>();
        models.Setup(s => s.GetByIdAsync(modelo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModeloCacheDto { Id = modelo.Id, Nombre = modelo.Nombre, MarcaId = marca.Id });
        var cars = new CreateCarCommandHandler(context, new FakeDateTimeProvider(), brands.Object, models.Object, Tenant(DealerId));
        return (context, cars, marca, modelo);
    }

    private static CreateCarCommand Car(Marca marca, Modelo modelo, string plate, decimal price, Guid? branchId) =>
        new(marca.Id, modelo.Id, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1800, 0, 2026, plate, "d", price, BranchId: branchId);

    [Fact]
    public async Task Branch_names_are_unique_per_dealer()
    {
        var (context, _, _, _) = await SeedAsync();
        using var _ = context;
        var handler = new CreateBranchCommandHandler(context, Tenant(DealerId));

        (await handler.Handle(new CreateBranchCommand("Norte"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new CreateBranchCommand("norte"), CancellationToken.None)).Error.Code.Should().Be("Branch.AlreadyExists");
    }

    [Fact]
    public async Task A_vehicle_goes_to_one_of_the_dealers_branches_and_stock_is_grouped_by_branch()
    {
        var (context, cars, marca, modelo) = await SeedAsync();
        using var _ = context;
        var centro = Branch.Create(DealerId, "Centro", isMain: true).Value;
        var norte = Branch.Create(DealerId, "Norte", isMain: false).Value;
        var foreign = Branch.Create(Guid.NewGuid(), "Ajena", isMain: true).Value;
        context.Branches.AddRange(centro, norte, foreign);
        await context.SaveChangesAsync();

        (await cars.Handle(Car(marca, modelo, "BRN001", 100m, norte.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await cars.Handle(Car(marca, modelo, "BRN002", 200m, norte.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await cars.Handle(Car(marca, modelo, "BRN003", 50m, centro.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var rejected = await cars.Handle(Car(marca, modelo, "BRN004", 1m, foreign.Id), CancellationToken.None);

        var stats = await new GetInventoryStatsQueryHandler(context).Handle(new GetInventoryStatsQuery(GroupByBranch: true), CancellationToken.None);

        rejected.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
        stats.Value.ByBranch.Should().BeEquivalentTo(new[]
        {
            new BranchInventory(norte.Id, "Norte", 2, 300m),
            new BranchInventory(centro.Id, "Centro", 1, 50m),
        });
    }
}
