using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Cars.GetAll;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.DealerSettings;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureEfTests.Cars;

/// <summary>
/// Proves the <c>GET /cars</c> sort actually reaches the database.
///
/// The unit suite for this handler runs on the EF in-memory provider, which evaluates
/// OrderBy client-side — it would stay green even for an expression no relational
/// provider can translate. These tests run the real <see cref="ApplicationDbContext"/>
/// on relational SQLite, so a non-translatable ordering fails here instead of silently
/// degrading in production.
///
/// <para>
/// <c>price</c> is deliberately absent from this file. SQLite has no decimal type and
/// refuses <c>decimal</c> in ORDER BY ("SQLite does not support expressions of type
/// 'decimal' in ORDER BY clauses"), which is a limitation of this harness, not of the
/// query: the ordering does translate, and Postgres orders the <c>numeric</c> column
/// natively. Price sorting is proven end-to-end against a real Postgres in
/// <c>WebApiTests/Postgres/GetAllCarsSortPostgresTests</c>.
/// </para>
/// </summary>
public class GetAllCarsSortTranslationTests
{
    private static readonly Guid TestDealerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private sealed class FakeCurrentTenantService : ICurrentTenantService
    {
        public Guid DealerId => TestDealerId;
        public bool HasTenant => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    /// <summary>
    /// Never called: every seeded car has zero images, so the handler's presign branch
    /// is unreachable. Present only to satisfy the constructor.
    /// </summary>
    private sealed class UnusedStorageService : IStorageService
    {
        public Task<string> UploadFileAsync(Stream stream, string objectKey, string contentType, long? size, CancellationToken ct) =>
            throw new NotSupportedException("Seeded cars carry no images.");

        public Task DeleteFileAsync(string objectKey, CancellationToken ct) =>
            throw new NotSupportedException("Seeded cars carry no images.");

        public Task<Uri> GetPresignedUrlAsync(string objectKey, TimeSpan ttl, CancellationToken ct) =>
            throw new NotSupportedException("Seeded cars carry no images.");

        public Task<(string Url, IReadOnlyDictionary<string, string> Fields)> GeneratePresignedPostAsync(
            string objectKey, string contentType, TimeSpan ttl, CancellationToken ct) =>
            throw new NotSupportedException("Seeded cars carry no images.");
    }

    private static async Task<(ApplicationDbContext Context, SqliteConnection Connection)> CreateContextAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var context = new ApplicationDbContext(options, new NoOpPublisher(), new FakeCurrentTenantService());
        await context.Database.EnsureCreatedAsync();

        context.DealerSettings.Add(new DealerSettings(TestDealerId, "Test Dealer", "test@dealer.com"));
        await context.SaveChangesAsync();

        return (context, connection);
    }

    private static async Task<(ApplicationDbContext Context, SqliteConnection Connection, Guid Cheap, Guid Mid, Guid Expensive)>
        SeedThreeCarsAsync()
    {
        var (context, connection) = await CreateContextAsync();

        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();

        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var cheap = new Car(
            TestDealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 90_000, 2020, "AAA111", "cheap",
            10_000m, baseDate.AddDays(3), FuelType.Gasolina, false, Transmission.Manual, null);

        var mid = new Car(
            TestDealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 40_000, 2023, "BBB222", "mid",
            20_000m, baseDate.AddDays(1), FuelType.Diesel, false, Transmission.Automatic, null);

        var expensive = new Car(
            TestDealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 10_000, 2018, "CCC333", "expensive",
            30_000m, baseDate.AddDays(2), FuelType.Electrico, false, Transmission.CVT, null);

        context.Cars.AddRange(cheap, mid, expensive);
        await context.SaveChangesAsync();

        return (context, connection, cheap.Id, mid.Id, expensive.Id);
    }

    private static GetAllCarsQueryHandler CreateHandler(ApplicationDbContext context) =>
        new(context, new UnusedStorageService());

    /// <summary>
    /// Every whitelisted field except price must survive translation. A field that only
    /// works on the in-memory provider throws InvalidOperationException here.
    /// </summary>
    [Theory]
    [InlineData("year")]
    [InlineData("kilometraje")]
    [InlineData("fuelType")]
    [InlineData("marca")]
    [InlineData("modelo")]
    [InlineData("status")]
    [InlineData("serviceStatus")]
    [InlineData("createdAt")]
    [InlineData("updatedAt")]
    public async Task EverySupportedSortField_Should_TranslateToSql(string sortBy)
    {
        var (context, connection, _, _, _) = await SeedThreeCarsAsync();
        await using var __ = context;
        await using var ___ = connection;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, sortBy, "asc"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Items.Count);
    }

    /// <summary>
    /// Sorting must happen before Skip/Take at the SQL level: each single-row page holds
    /// the correct slice of the globally sorted inventory. Sorts by year (kilometraje and
    /// year both order the three seeded cars distinctly) since price is untestable here.
    /// </summary>
    [Fact]
    public async Task Paging_Should_SliceAnAlreadySortedSet()
    {
        var (context, connection, cheap, mid, expensive) = await SeedThreeCarsAsync();
        await using var _ = context;
        await using var __ = connection;

        var handler = CreateHandler(context);

        // year asc: expensive(2018) < cheap(2020) < mid(2023)
        var page1 = await handler.Handle(new GetAllCarsQuery(1, 1, "year", "asc"), CancellationToken.None);
        var page2 = await handler.Handle(new GetAllCarsQuery(2, 1, "year", "asc"), CancellationToken.None);
        var page3 = await handler.Handle(new GetAllCarsQuery(3, 1, "year", "asc"), CancellationToken.None);

        Assert.Equal(expensive, Assert.Single(page1.Value.Items).Id);
        Assert.Equal(cheap, Assert.Single(page2.Value.Items).Id);
        Assert.Equal(mid, Assert.Single(page3.Value.Items).Id);
        Assert.Equal(3, page1.Value.TotalCount);
    }

    [Fact]
    public async Task FuelTypeAndTransmission_Should_RoundTripThroughTheDatabase()
    {
        var (context, connection, cheap, mid, expensive) = await SeedThreeCarsAsync();
        await using var _ = context;
        await using var __ = connection;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, "year", "asc"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var items = result.Value.Items;

        Assert.Equal(FuelType.Gasolina, items.Single(c => c.Id == cheap).FuelType);
        Assert.Equal(FuelType.Diesel, items.Single(c => c.Id == mid).FuelType);
        Assert.Equal(FuelType.Electrico, items.Single(c => c.Id == expensive).FuelType);

        Assert.Equal(Transmission.Manual, items.Single(c => c.Id == cheap).Transmission);
        Assert.Equal(Transmission.Automatic, items.Single(c => c.Id == mid).Transmission);
        Assert.Equal(Transmission.CVT, items.Single(c => c.Id == expensive).Transmission);
    }
}
