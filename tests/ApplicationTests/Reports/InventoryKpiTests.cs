using Application.Abstractions.Tenancy;
using Application.Cars.Kpis;
using Application.Queries.Cars.GetInventoryStats;
using Application.Reports.GetSalesFunnel;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Deposits;
using Domain.Financial.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Sales;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuotePaymentMethod = Domain.Quotes.Attributes.PaymentMethod;

namespace Application.UnitTests.Reports;

/// <summary>
/// sfc B6.1 / RN-042 (enterprise-erp-crm "KPIs de inventario y funnel completo"): days in stock from entry to sale (or
/// today), unit margin net of purchase cost and reconditioning, the full Lead → Quote → Deposit → Sale funnel, and the
/// EnVenta cars no longer missing from the inventory stats.
/// </summary>
public class InventoryKpiTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);

    private static ICurrentTenantService Tenant()
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return tenant.Object;
    }

    private static Car NewCar(TestApplicationDbContext context, string plate, DateTime enteredAt, decimal price,
        StatusServiceCar status = StatusServiceCar.Disponible, decimal? purchaseCost = null)
    {
        var marca = new Marca("Toyota " + plate);
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, status,
            4, 5, 1600, 1000, 2021, plate, "desc", price, enteredAt, purchaseCost: purchaseCost);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    [Fact]
    public async Task DaysInStock_Should_RunUntilToday_ForAnUnsoldCar()
    {
        using var context = CreateContext();
        var car = NewCar(context, "KPI001", new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), 10000m);
        await context.SaveChangesAsync();
        var clock = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 2, 1, 18, 0, 0, DateTimeKind.Utc) };

        var result = await new GetInventoryKpisQueryHandler(context, Tenant(), clock).Handle(new GetInventoryKpisQuery(), CancellationToken.None);

        var kpi = result.Value.Single(k => k.CarId == car.Id);
        kpi.DaysInStock.Should().Be(31);
        kpi.SoldAtUtc.Should().BeNull();
        kpi.Margin.Should().BeNull();
    }

    [Fact]
    public async Task Margin_Should_DiscountPurchaseCostAndReconditioning_AndDaysInStockStopAtTheSale()
    {
        using var context = CreateContext();
        var entered = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var car = NewCar(context, "KPI002", entered, 3_000_000m, StatusServiceCar.Vendido, purchaseCost: 2_500_000m);
        var task = car.AddReconditioningTask("Pintura", Money.Of(57_000m, CurrencyCode.Ars));
        car.CompleteReconditioningTask(task.Id, entered.AddDays(3));
        var sale = new Sale(DealerId, car.Id, Guid.NewGuid(), 3_000_000m, PaymentMethod.Cash, "C-1", "c", entered.AddDays(20));
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        var clock = new FakeDateTimeProvider { UtcNow = entered.AddDays(60) };

        var result = await new GetInventoryKpisQueryHandler(context, Tenant(), clock).Handle(new GetInventoryKpisQuery(), CancellationToken.None);

        var kpi = result.Value.Single(k => k.CarId == car.Id);
        kpi.Margin.Should().Be(443_000m);
        kpi.Currency.Should().Be("ARS");
        kpi.DaysInStock.Should().Be(20);
        kpi.SoldAtUtc.Should().Be(entered.AddDays(20));
    }

    [Fact]
    public async Task InventoryStats_Should_CountEnVentaCarsAsAvailable()
    {
        using var context = CreateContext();
        NewCar(context, "KPI003", DateTime.UtcNow, 1m, StatusServiceCar.Disponible);
        NewCar(context, "KPI004", DateTime.UtcNow, 1m, StatusServiceCar.EnVenta);
        NewCar(context, "KPI005", DateTime.UtcNow, 1m, StatusServiceCar.Reservado);
        await context.SaveChangesAsync();

        var result = await new GetInventoryStatsQueryHandler(context).Handle(new GetInventoryStatsQuery(), CancellationToken.None);

        result.Value.ByStatus.Available.Should().Be(2);
        result.Value.ByStatus.Reserved.Should().Be(1);
    }

    [Fact]
    public async Task Funnel_Should_CountEachStage_WithinTheRange()
    {
        using var context = CreateContext();
        var day = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
        var car = NewCar(context, "KPI006", day.AddDays(-30), 10000m);
        context.Leads.AddRange(
            Lead.Create(DealerId, "A", "a@x.com", "1", LeadSource.Web, day),
            Lead.Create(DealerId, "B", "b@x.com", "2", LeadSource.Web, day),
            Lead.Create(DealerId, "Old", "o@x.com", "3", LeadSource.Web, day.AddMonths(-3)));
        var client = new Domain.Clients.Client(DealerId, "Ana", "Perez", "30111222", "ana@x.com", "1", "Addr", day);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        context.Quotes.Add(new Quote(DealerId, car, client, null, 10000m, QuotePaymentMethod.Contado, day.AddDays(10), "q", day));
        context.Deposits.Add(Deposit.Create(DealerId, car.Id, client.Id, null, 100m, "ARS", null, day,
            new DepositCarContext(StatusServiceCar.Disponible, null, false, "ARS")).Value);
        var completed = new Sale(DealerId, car.Id, client.Id, 10000m, PaymentMethod.Cash, "C-2", "c", day);
        completed.Complete();
        context.Sales.AddRange(completed, new Sale(DealerId, car.Id, client.Id, 10000m, PaymentMethod.Cash, "C-3", "c", day));
        await context.SaveChangesAsync();

        var result = await new GetSalesFunnelQueryHandler(context, Tenant())
            .Handle(new GetSalesFunnelQuery(day.AddDays(-5), day.AddDays(5)), CancellationToken.None);

        result.Value.Should().Be(new SalesFunnelResponse(Leads: 2, Quotes: 1, Deposits: 1, Sales: 1));
    }
}
