using Application.Abstractions.Authentication;
using Application.Abstractions.Branches;
using Application.Abstractions.Storage;
using Application.Cars.GetAll;
using Application.Cars.GetById;
using Application.Cars.GetByIds;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Branches;

/// <summary>
/// sfc C2.2 / RN-048 / ADR C26: a role with branch:scoped only lists the vehicles of its own branches; without the
/// restriction (Admin, or any role without branch:scoped) every vehicle of the dealer is listed.
/// </summary>
public class BranchScopeTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Norte = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Centro = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private static async Task<TestApplicationDbContext> SeedAsync()
    {
        var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        foreach (var (plate, branch) in new[] { ("SCP001", (Guid?)Norte), ("SCP002", Norte), ("SCP003", Centro), ("SCP004", null) })
        {
            var car = new Car(DealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
                4, 5, 1800, 0, 2026, plate, "d", 1m, DateTime.UtcNow);
            car.AssignBranch(branch);
            context.Cars.Add(car);
        }
        await context.SaveChangesAsync();
        return context;
    }

    private static GetAllCarsQueryHandler Handler(TestApplicationDbContext context, IReadOnlyCollection<Guid>? restrictedTo)
    {
        var scope = new Mock<IBranchScope>();
        scope.Setup(s => s.GetRestrictedBranchIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(restrictedTo);
        return new GetAllCarsQueryHandler(context, new Mock<IStorageService>().Object, new Mock<IUserContext>().Object, scope.Object);
    }

    [Fact]
    public async Task A_branch_scoped_user_only_lists_its_branches()
    {
        using var context = await SeedAsync();

        var result = await Handler(context, [Norte]).Handle(new GetAllCarsQuery(1, 50), CancellationToken.None);

        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(c => c.Patente).Should().BeEquivalentTo("SCP001", "SCP002");
    }

    [Fact]
    public async Task Without_restriction_every_vehicle_of_the_dealer_is_listed()
    {
        using var context = await SeedAsync();

        var result = await Handler(context, restrictedTo: null).Handle(new GetAllCarsQuery(1, 50), CancellationToken.None);

        result.Value.TotalCount.Should().Be(4);
    }

    private static Mock<IBranchScope> Scope(IReadOnlyCollection<Guid>? restrictedTo)
    {
        var scope = new Mock<IBranchScope>();
        scope.Setup(s => s.GetRestrictedBranchIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(restrictedTo);
        return scope;
    }

    private static Mock<IUserContext> SignedIn()
    {
        var user = new Mock<IUserContext>();
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        return user;
    }

    // Sibling read paths are anonymous (public catalog), so the vehicle itself stays visible; what the scope
    // withholds is the plate, which only signed-in staff see and which a scoped user must not get for another branch.
    [Fact]
    public async Task The_vehicle_detail_hides_the_plate_of_another_branch_from_a_scoped_user()
    {
        using var context = await SeedAsync();
        Guid centroCar = context.Cars.Single(c => c.BranchId == Centro).Id;
        Guid norteCar = context.Cars.First(c => c.BranchId == Norte).Id;
        var handler = new GetCarByIdQueryHandler(context, new Mock<IStorageService>().Object, SignedIn().Object, Scope([Norte]).Object);

        (await handler.Handle(new GetCarByIdQuery(centroCar), CancellationToken.None)).Value.Patente.Should().BeNull();
        (await handler.Handle(new GetCarByIdQuery(norteCar), CancellationToken.None)).Value.Patente.Should().NotBeNull();
    }

    [Fact]
    public async Task The_bulk_lookup_hides_the_plate_of_another_branch_from_a_scoped_user()
    {
        using var context = await SeedAsync();
        List<Guid> ids = context.Cars.Select(c => c.Id).ToList();
        var handler = new GetCarsByIdsQueryHandler(context, new Mock<IStorageService>().Object, SignedIn().Object, Scope([Norte]).Object);

        var result = await handler.Handle(new GetCarsByIdsQuery(ids), CancellationToken.None);

        result.Value.Where(c => c.Patente != null).Should().HaveCount(2);
    }
}
