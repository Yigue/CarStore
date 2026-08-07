using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Postgres;

/// <summary>
/// End-to-end guard for the <c>GET /api/v1/cars</c> sort contract against a real
/// migrated Postgres.
///
/// <para>
/// The endpoint used to bind neither <c>sortBy</c> nor <c>sortOrder</c>: every
/// combination returned byte-identical ordering and <c>sortBy=NotARealField</c> still
/// answered 200, so a caller could not distinguish a working sort from a typo.
/// </para>
///
/// <para>
/// Postgres — not SQLite — is the only harness that can prove the <c>price</c> sort.
/// Price is a Money value object behind a ValueConverter over a single decimal column;
/// SQLite has no decimal type and rejects <c>decimal</c> in ORDER BY, while Postgres
/// orders the <c>numeric</c> column natively, exactly as production does.
/// </para>
/// </summary>
[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class GetAllCarsSortPostgresTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    /// <summary>Distinct plates so the seeded trio is identifiable among seeder data.</summary>
    private const string CheapPlate = "SRT001";
    private const string MidPlate = "SRT002";
    private const string ExpensivePlate = "SRT003";

    private Guid _cheapId;
    private Guid _midId;
    private Guid _expensiveId;

    public GetAllCarsSortPostgresTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
        await SeedSortableCarsAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Runs once per test in this class against the collection-shared Postgres container,
    /// so it must be idempotent: <c>Patente</c> carries a unique index and re-inserting
    /// the same plates would fail every test after the first. Existing rows are reused.
    /// </summary>
    private async Task SeedSortableCarsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Filtered in memory on purpose: Patente is a LicensePlate value object behind a
        // ValueConverter, so `c.Patente.Value == plate` does not translate to SQL. The
        // test database holds only seeder rows plus these three.
        var allCars = await db.Cars.IgnoreQueryFilters().ToListAsync();
        var existing = allCars
            .Where(c => c.Patente.Value is CheapPlate or MidPlate or ExpensivePlate)
            .ToList();

        if (existing.Count == 3)
        {
            _cheapId = existing.Single(c => c.Patente.Value == CheapPlate).Id;
            _midId = existing.Single(c => c.Patente.Value == MidPlate).Id;
            _expensiveId = existing.Single(c => c.Patente.Value == ExpensivePlate).Id;
            return;
        }

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);

        var marca = await db.Marca.IgnoreQueryFilters().FirstOrDefaultAsync();
        if (marca is null)
        {
            marca = new Marca("SortTest");
            db.Marca.Add(marca);
            await db.SaveChangesAsync();
        }

        var modelo = await db.Modelo.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.MarcaId == marca.Id);
        if (modelo is null)
        {
            modelo = new Modelo("SortModel", marca.Id);
            db.Modelo.Add(modelo);
            await db.SaveChangesAsync();
        }

        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // price asc: cheap < mid < expensive — and no other seeded car sits between them,
        // because the assertions only check the relative order of these three.
        var cheap = new Car(
            dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 90_000, 2020, CheapPlate, "cheap",
            11_111m, baseDate.AddDays(3), FuelType.Gasolina, false, Transmission.Manual, null);

        var mid = new Car(
            dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 40_000, 2023, MidPlate, "mid",
            22_222m, baseDate.AddDays(1), FuelType.Diesel, false, Transmission.Automatic, null);

        var expensive = new Car(
            dealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 10_000, 2018, ExpensivePlate, "expensive",
            33_333m, baseDate.AddDays(2), FuelType.Electrico, false, Transmission.CVT, null);

        db.Cars.AddRange(cheap, mid, expensive);
        await db.SaveChangesAsync();

        _cheapId = cheap.Id;
        _midId = mid.Id;
        _expensiveId = expensive.Id;
    }

    private sealed record CarRow(Guid Id, string Patente, decimal Precio, string FuelType, string Transmission);

    private sealed record CarsPage(List<CarRow> Items, int TotalCount, int Page, int PageSize);

    private async Task<CarsPage> GetCarsAsync(string queryString)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri($"/api/v1/cars?{queryString}", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CarsPage>(IntegrationTestHelpers.JsonOptions))!;
    }

    /// <summary>Relative positions of the three seeded cars within a returned page.</summary>
    private (int Cheap, int Mid, int Expensive) IndicesOf(CarsPage page)
    {
        var ids = page.Items.Select(i => i.Id).ToList();
        return (ids.IndexOf(_cheapId), ids.IndexOf(_midId), ids.IndexOf(_expensiveId));
    }

    [Fact]
    public async Task Price_Ascending_Should_OrderCheapestFirst()
    {
        var page = await GetCarsAsync("page=1&pageSize=1000&sortBy=price&sortOrder=asc");

        var (cheap, mid, expensive) = IndicesOf(page);
        cheap.Should().BeGreaterThanOrEqualTo(0);
        cheap.Should().BeLessThan(mid);
        mid.Should().BeLessThan(expensive);
    }

    [Fact]
    public async Task Price_Descending_Should_OrderMostExpensiveFirst()
    {
        var page = await GetCarsAsync("page=1&pageSize=1000&sortBy=price&sortOrder=desc");

        var (cheap, mid, expensive) = IndicesOf(page);
        expensive.Should().BeGreaterThanOrEqualTo(0);
        expensive.Should().BeLessThan(mid);
        mid.Should().BeLessThan(cheap);
    }

    /// <summary>
    /// The original defect in one assertion: asc and desc used to return byte-identical
    /// payloads because the parameters were never bound.
    /// </summary>
    [Fact]
    public async Task AscAndDesc_Should_NotReturnTheSameOrder()
    {
        var asc = await GetCarsAsync("page=1&pageSize=1000&sortBy=price&sortOrder=asc");
        var desc = await GetCarsAsync("page=1&pageSize=1000&sortBy=price&sortOrder=desc");

        asc.Items.Select(i => i.Id).Should().NotEqual(desc.Items.Select(i => i.Id));
    }

    [Theory]
    [InlineData("price")]
    [InlineData("year")]
    [InlineData("anio")]
    [InlineData("kilometraje")]
    [InlineData("fuelType")]
    [InlineData("marca")]
    [InlineData("modelo")]
    [InlineData("status")]
    [InlineData("serviceStatus")]
    [InlineData("createdAt")]
    [InlineData("updatedAt")]
    public async Task EverySupportedSortField_Should_Return200_OnPostgres(string sortBy)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri($"/api/v1/cars?sortBy={sortBy}&sortOrder=asc", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownSortField_Should_Return400_InsteadOfSilently200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/cars?sortBy=NotARealField", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("NotARealField");
    }

    [Fact]
    public async Task UnknownSortOrder_Should_Return400()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/cars?sortBy=price&sortOrder=sideways", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NoSortParameters_Should_StillReturn200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/cars", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Sorting is applied before paging, so a one-row page of a price-descending sort
    /// holds the globally most expensive car — never the first row of an unsorted page.
    /// </summary>
    [Fact]
    public async Task Paging_Should_SliceTheGloballySortedSet()
    {
        var firstPage = await GetCarsAsync("page=1&pageSize=1&sortBy=price&sortOrder=desc");
        var everything = await GetCarsAsync("page=1&pageSize=1000&sortBy=price&sortOrder=desc");

        firstPage.Items.Should().ContainSingle();
        firstPage.Items[0].Id.Should().Be(everything.Items[0].Id);
        firstPage.TotalCount.Should().Be(everything.TotalCount);
    }

    /// <summary>
    /// The dashboard's "Combustible" column read a field the payload never carried, so
    /// it rendered "—" on every row regardless of the vehicle's actual fuel type.
    /// </summary>
    [Fact]
    public async Task Payload_Should_CarryFuelTypeAndTransmission()
    {
        var page = await GetCarsAsync("page=1&pageSize=1000&sortBy=price&sortOrder=asc");

        var cheap = page.Items.Single(i => i.Patente == CheapPlate);
        var mid = page.Items.Single(i => i.Patente == MidPlate);
        var expensive = page.Items.Single(i => i.Patente == ExpensivePlate);

        cheap.FuelType.Should().Be("Gasolina");
        mid.FuelType.Should().Be("Diesel");
        expensive.FuelType.Should().Be("Electrico");

        cheap.Transmission.Should().Be("Manual");
        mid.Transmission.Should().Be("Automatic");
        expensive.Transmission.Should().Be("CVT");
    }
}
