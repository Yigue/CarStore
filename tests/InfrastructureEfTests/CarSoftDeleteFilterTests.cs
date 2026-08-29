using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Withdrawing a vehicle only works if the row then disappears from every read path. A soft
/// delete without a matching global filter is worse than no soft delete at all: the operator
/// presses delete, gets a success, and the vehicle is still in the public catalogue.
/// </summary>
public class CarSoftDeleteFilterTests
{
    private static readonly Guid TestDealerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class TenantService : ICurrentTenantService
    {
        public Guid DealerId => TestDealerId;
        public bool HasTenant => true;
    }

    private static async Task<(ApplicationDbContext Context, SqliteConnection Connection)> CreateContextAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var context = new ApplicationDbContext(options, new NoOpPublisher(), new TenantService());
        await context.Database.EnsureCreatedAsync();

        // Most entities carry a DealerId FK, so the tenant row has to exist first.
        context.DealerSettings.Add(
            new Domain.DealerSettings.DealerSettings(TestDealerId, "Test Dealer", "test@dealer.com"));
        await context.SaveChangesAsync();

        return (context, connection);
    }

    private static async Task<Car> BuildCarAsync(ApplicationDbContext context, string patente)
    {
        var marca = new Marca($"Chevrolet-{patente}");
        var modelo = new Modelo($"Onix-{patente}", marca.Id);
        context.AddRange(marca, modelo);
        await context.SaveChangesAsync();

        return new Car(
            TestDealerId, marca, modelo, Color.Black, TypeCar.Hatchback, StatusCar.Used,
            StatusServiceCar.Disponible, 5, 5, 1400, 20000, 2022, patente, "filter test",
            11000m, DateTime.UtcNow);
    }

    [Fact]
    public async Task WithdrawnCar_Should_BeInvisibleToNormalQueries()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        Car live = await BuildCarAsync(context, "VIS001");
        Car withdrawn = await BuildCarAsync(context, "HID001");
        withdrawn.SoftDelete(DateTime.UtcNow);

        context.Cars.AddRange(live, withdrawn);
        await context.SaveChangesAsync();

        List<Guid> visible = await context.Cars.Select(c => c.Id).ToListAsync();

        visible.Should().ContainSingle().Which.Should().Be(live.Id);
    }

    [Fact]
    public async Task WithdrawnCar_Should_StillBeStored_SoReferencesRemainValid()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        Car withdrawn = await BuildCarAsync(context, "HID002");
        withdrawn.SoftDelete(DateTime.UtcNow);
        context.Cars.Add(withdrawn);
        await context.SaveChangesAsync();

        Car? stored = await context.Cars
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == withdrawn.Id);

        stored.Should().NotBeNull("a Quote or Sale foreign key still points at this row");
        stored!.IsDeleted.Should().BeTrue();
        stored.DeletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task RestoredCar_Should_BecomeVisibleAgain()
    {
        var (context, connection) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        Car car = await BuildCarAsync(context, "RES001");
        car.SoftDelete(DateTime.UtcNow);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        (await context.Cars.AnyAsync(c => c.Id == car.Id)).Should().BeFalse();

        Car stored = await context.Cars.IgnoreQueryFilters().SingleAsync(c => c.Id == car.Id);
        stored.Restore(DateTime.UtcNow);
        await context.SaveChangesAsync();

        (await context.Cars.AnyAsync(c => c.Id == car.Id)).Should().BeTrue();
    }
}
