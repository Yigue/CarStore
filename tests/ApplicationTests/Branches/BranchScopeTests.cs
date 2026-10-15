using Application.Abstractions.Authentication;
using Application.Abstractions.Branches;
using Application.Abstractions.Storage;
using Application.Cars.GetAll;
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
}
