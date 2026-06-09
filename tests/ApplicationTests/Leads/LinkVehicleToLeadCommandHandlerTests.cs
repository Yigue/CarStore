using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.LinkVehicle;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

public class LinkVehicleToLeadCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldLinkVehicle_WhenBothExist()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1800, 0, 2024, "TYT001", "desc", 25000m, DateTime.UtcNow);
        var lead = Lead.Create(dealerId, "Bob Jones", "bob@test.com", "9876543", LeadSource.Web, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var handler = new LinkVehicleToLeadCommandHandler(context);
        var command = new LinkVehicleToLeadCommand(lead.Id, car.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Leads.FindAsync(lead.Id);
        updated!.InterestedVehicleId.Should().Be(car.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenLeadNotFound()
    {
        using var context = CreateContext();
        var handler = new LinkVehicleToLeadCommandHandler(context);
        var command = new LinkVehicleToLeadCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
