using Application.Abstractions.Tenancy;
using Application.Cars.Consignment;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Cars;

/// <summary>sfc B7.1 / RN-036: a car goes on consignment for one of the dealer's clients; another dealer's client is 404.</summary>
public class SetConsignmentCommandTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherDealerId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task Places_the_car_on_consignment_only_for_one_of_the_dealers_clients()
    {
        using var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var marca = new Marca("Ford");
        var modelo = new Modelo("Ka", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 1400, 1, 2018, "CSG100", "d", 1m, DateTime.UtcNow);
        var mine = new Client(DealerId, "A", "B", "1", "a@x.com", "1", "x", DateTime.UtcNow);
        var foreign = new Client(OtherDealerId, "C", "D", "2", "c@x.com", "1", "x", DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.AddRange(mine, foreign);
        await context.SaveChangesAsync();
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        var handler = new SetConsignmentCommandHandler(context, tenant.Object);

        var rejected = await handler.Handle(new SetConsignmentCommand(car.Id, foreign.Id, 0.1m), CancellationToken.None);
        var placed = await handler.Handle(new SetConsignmentCommand(car.Id, mine.Id, 0.1m), CancellationToken.None);
        var ended = await handler.Handle(new SetConsignmentCommand(car.Id, null, null), CancellationToken.None);

        rejected.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
        placed.IsSuccess.Should().BeTrue();
        ended.IsSuccess.Should().BeTrue();
        (await context.Cars.SingleAsync()).IsConsigned.Should().BeFalse();
    }
}
