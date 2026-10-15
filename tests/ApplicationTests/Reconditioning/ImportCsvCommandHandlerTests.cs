using Application.Abstractions.Tenancy;
using Application.Reconditioning.Csv;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Reconditioning;

/// <summary>
/// sfc B7.3 / RN-038 (reconditioning-csv-import-export): import validates headers for the whole file and each row on its
/// own, so bad rows are reported without dropping the good ones; export returns one row per task.
/// </summary>
public class ImportCsvCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ICurrentTenantService Tenant(Guid dealerId)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        return tenant.Object;
    }

    private static async Task<(TestApplicationDbContext Context, Car Car)> SeedAsync()
    {
        var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var marca = new Marca("Ford");
        var modelo = new Modelo("Ka", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 1400, 1, 2018, "REC001", "d", 1m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return (context, car);
    }

    private static ImportReconditioningCsvCommandHandler Import(TestApplicationDbContext context, Guid? dealerId = null) =>
        new(context, Tenant(dealerId ?? DealerId));

    private static Task<List<ReconditioningTask>> TasksAsync(TestApplicationDbContext context) =>
        context.ReconditioningTasks.ToListAsync();

    [Fact]
    public async Task A_valid_file_creates_every_task_as_pending()
    {
        var (context, car) = await SeedAsync();
        using var _ = context;
        const string csv = "description,cost\nPintura,1000\nFrenos,2500.50\n\"Tapizado, asientos\",800\nAlineación,300\nLavado,150\n";

        var result = await Import(context).Handle(new ImportReconditioningCsvCommand(car.Id, csv), CancellationToken.None);

        result.Value.Created.Should().Be(5);
        result.Value.Errors.Should().BeEmpty();
        (await TasksAsync(context)).Should().HaveCount(5).And.OnlyContain(t => t.Status == ReconditioningStatus.Pending);
        (await TasksAsync(context)).Should().Contain(t => t.Description == "Tapizado, asientos" && t.Cost.Amount == 800m);
    }

    [Fact]
    public async Task Invalid_rows_are_reported_and_the_valid_ones_still_created()
    {
        var (context, car) = await SeedAsync();
        using var _ = context;
        const string csv = "Description;Cost\nPintura;1000\nFrenos;caro\n;50\nAlineación;300\nLavado;-1\nTapizado;800\n";

        var result = await Import(context).Handle(new ImportReconditioningCsvCommand(car.Id, csv), CancellationToken.None);

        result.Value.Created.Should().Be(3);
        result.Value.Errors.Select(e => e.Row).Should().Equal(3, 4, 6);
        result.Value.Errors[0].Message.Should().Contain("Costo inválido en la fila 3");
        result.Value.Errors[1].Message.Should().Contain("Falta la descripción en la fila 4");
        (await TasksAsync(context)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Wrong_headers_reject_the_whole_file()
    {
        var (context, car) = await SeedAsync();
        using var _ = context;

        var result = await Import(context).Handle(new ImportReconditioningCsvCommand(car.Id, "description,price\nPintura,1000\n"), CancellationToken.None);

        result.Error.Code.Should().Be("INVALID_CSV_HEADERS");
        (await TasksAsync(context)).Should().BeEmpty();
    }

    [Fact]
    public async Task Another_dealers_car_is_not_found()
    {
        var (context, car) = await SeedAsync();
        using var _ = context;

        var result = await Import(context, Guid.NewGuid()).Handle(new ImportReconditioningCsvCommand(car.Id, "description,cost\nX,1\n"), CancellationToken.None);

        result.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
    }

    [Fact]
    public async Task Export_returns_one_row_per_task_with_description_cost_and_status()
    {
        var (context, car) = await SeedAsync();
        using var _ = context;
        await Import(context).Handle(new ImportReconditioningCsvCommand(car.Id, "description,cost\nPintura,1000\n\"Tapizado, asientos\",800\n"), CancellationToken.None);

        var csv = await new GetReconditioningCsvQueryHandler(context, Tenant(DealerId))
            .Handle(new GetReconditioningCsvQuery(car.Id), CancellationToken.None);

        string[] lines = csv.Value.TrimEnd('\n').Split('\n');
        lines[0].Should().Be("description,cost,currency,status");
        lines.Skip(1).Should().BeEquivalentTo("Pintura,1000.00,ARS,Pending", "\"Tapizado, asientos\",800.00,ARS,Pending");
    }
}
