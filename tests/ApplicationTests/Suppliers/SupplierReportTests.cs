using Application.Abstractions.Caching;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Application.Cars.Create;
using Application.Reports.GetPurchasesBySupplier;
using Application.Suppliers.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Suppliers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Suppliers;

/// <summary>
/// sfc B7.4 / RN-039: suppliers are unique by name per dealer; a vehicle can be bought from one of the dealer's
/// suppliers; the purchases report adds up PurchaseCost per supplier (and per currency) within the period.
/// </summary>
public class SupplierReportTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherDealerId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ICurrentTenantService Tenant(Guid dealerId)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        return tenant.Object;
    }

    private static async Task<(CreateCarCommandHandler Handler, Marca Marca, Modelo Modelo)> CarHandlerAsync(TestApplicationDbContext context)
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Hilux", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();
        var brands = new Mock<ICachedBrandService>();
        brands.Setup(s => s.GetByIdAsync(marca.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new MarcaCacheDto { Id = marca.Id, Nombre = marca.Nombre });
        var models = new Mock<ICachedModelService>();
        models.Setup(s => s.GetByIdAsync(modelo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModeloCacheDto { Id = modelo.Id, Nombre = modelo.Nombre, MarcaId = marca.Id });
        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(s => s.EnsureCanAddAsync(It.IsAny<Guid>(), It.IsAny<PlanResource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return (new CreateCarCommandHandler(context, context, new FakeDateTimeProvider { UtcNow = Now }, brands.Object, models.Object, Tenant(DealerId), entitlementService.Object),
            marca, modelo);
    }

    private static CreateCarCommand Car(Marca marca, Modelo modelo, string plate, decimal cost, Guid? supplierId) =>
        new(marca.Id, modelo.Id, Color.White, TypeCar.Pickup, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 2800, 10000, 2022, plate, "d", 30000m, PurchaseCost: cost, SupplierId: supplierId);

    [Fact]
    public async Task Supplier_names_are_unique_per_dealer_regardless_of_case()
    {
        using var context = CreateContext();
        var handler = new CreateSupplierCommandHandler(context, Tenant(DealerId));

        (await handler.Handle(new CreateSupplierCommand("Concesionaria Norte", null, null), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var duplicate = await handler.Handle(new CreateSupplierCommand("  concesionaria NORTE ", null, null), CancellationToken.None);
        var otherDealer = await new CreateSupplierCommandHandler(context, Tenant(OtherDealerId))
            .Handle(new CreateSupplierCommand("Concesionaria Norte", null, null), CancellationToken.None);

        duplicate.Error.Code.Should().Be("Supplier.AlreadyExists");
        otherDealer.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_vehicle_can_only_be_linked_to_one_of_the_dealers_suppliers()
    {
        using var context = CreateContext();
        var foreign = Supplier.Create(OtherDealerId, "Ajeno", null, null).Value;
        var mine = Supplier.Create(DealerId, "Propio", null, null).Value;
        context.Suppliers.AddRange(foreign, mine);
        await context.SaveChangesAsync();
        var (handler, marca, modelo) = await CarHandlerAsync(context);

        var linked = await handler.Handle(Car(marca, modelo, "SUP001", 20000m, mine.Id), CancellationToken.None);
        var rejected = await handler.Handle(Car(marca, modelo, "SUP002", 20000m, foreign.Id), CancellationToken.None);

        (await context.Cars.SingleAsync(c => c.Id == linked.Value)).SupplierId.Should().Be(mine.Id);
        rejected.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
    }

    [Fact]
    public async Task Purchases_report_adds_up_purchase_cost_per_supplier_within_the_period()
    {
        using var context = CreateContext();
        var norte = Supplier.Create(DealerId, "Concesionaria Norte", null, null).Value;
        context.Suppliers.Add(norte);
        await context.SaveChangesAsync();
        var (handler, marca, modelo) = await CarHandlerAsync(context);
        foreach (var (plate, cost) in new[] { ("SUP010", 10000m), ("SUP011", 12000m), ("SUP012", 8000m) })
            (await handler.Handle(Car(marca, modelo, plate, cost, norte.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await handler.Handle(Car(marca, modelo, "SUP013", 5000m, supplierId: null), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var result = await new GetPurchasesBySupplierQueryHandler(context, Tenant(DealerId))
            .Handle(new GetPurchasesBySupplierQuery(Now.AddDays(-30), Now.AddDays(1)), CancellationToken.None);

        result.Value.Should().ContainSingle()
            .Which.Should().Be(new SupplierPurchasesResponse(norte.Id, "Concesionaria Norte", "ARS", 3, 30000m));
    }
}
