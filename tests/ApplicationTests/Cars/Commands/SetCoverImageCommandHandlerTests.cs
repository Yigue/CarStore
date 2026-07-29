using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Cars.Commands.SetCoverImage;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;

namespace Application.UnitTests.Cars.Commands;

public class SetCoverImageCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Car CreateTestCar(Guid dealerId)
    {
        var marca = new Marca("Test");
        var modelo = new Modelo("Test", marca.Id);
        return new Car(
            dealerId,
            marca,
            modelo,
            Color.Blue,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
            4,
            5,
            2000,
            0,
            2024,
            $"T{Guid.NewGuid().ToString().Substring(0, 5)}", // unique patente
            "Test car",
            25000m,
            DateTime.UtcNow
        );
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCarDoesNotExist()
    {
        using var context = CreateContext();
        var mockTenant = new Mock<ICurrentTenantService>();
        mockTenant.Setup(t => t.DealerId).Returns(Guid.NewGuid());

        var handler = new SetCoverImageCommandHandler(context, mockTenant.Object);
        var command = new SetCoverImageCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CarErrors.NotFound(command.CarId).Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCarBelongsToAnotherTenant()
    {
        using var context = CreateContext();
        var anotherDealerId = Guid.NewGuid();
        var car = CreateTestCar(anotherDealerId);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var mockTenant = new Mock<ICurrentTenantService>();
        mockTenant.Setup(t => t.DealerId).Returns(Guid.NewGuid()); // Different dealer

        var handler = new SetCoverImageCommandHandler(context, mockTenant.Object);
        var command = new SetCoverImageCommand(car.Id, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CarErrors.NotFound(car.Id).Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnImageNotFoundInCar_WhenImageIsForeign()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var car = CreateTestCar(dealerId);
        var imgId = Guid.NewGuid();
        // The car has NO images
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var mockTenant = new Mock<ICurrentTenantService>();
        mockTenant.Setup(t => t.DealerId).Returns(dealerId);

        var handler = new SetCoverImageCommandHandler(context, mockTenant.Object);
        var command = new SetCoverImageCommand(car.Id, imgId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CarErrors.ImageNotFoundInCar(imgId, car.Id).Code);
    }

    [Fact]
    public async Task Handle_Should_DemotePreviousCoverAndPromoteTarget_OnSuccess()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var car = CreateTestCar(dealerId);
        
        car.Images.Add(new CarImage(car.Id, "url1", true, 0));
        car.Images.Add(new CarImage(car.Id, "url2", false, 1));
        
        var img1 = car.Images.First();
        var img2 = car.Images.Last();
        
        // Ensure img1 is currently cover, and img2 is not
        img1.SetAsCover(true);
        img2.SetAsCover(false);

        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var mockTenant = new Mock<ICurrentTenantService>();
        mockTenant.Setup(t => t.DealerId).Returns(dealerId);

        var handler = new SetCoverImageCommandHandler(context, mockTenant.Object);
        var command = new SetCoverImageCommand(car.Id, img2.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        
        var reloadedCar = await context.Cars.Include(c => c.Images).FirstAsync(c => c.Id == car.Id);
        reloadedCar.Images.Single(i => i.Id == img1.Id).IsCover.Should().BeFalse();
        reloadedCar.Images.Single(i => i.Id == img2.Id).IsCover.Should().BeTrue();
        reloadedCar.Images.Count(i => i.IsCover).Should().Be(1);
    }
}
