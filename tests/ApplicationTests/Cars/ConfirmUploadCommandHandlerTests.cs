using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Cars.Commands.ConfirmImageUpload;
using Application.Cars.Commands.UploadCarImage;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Cars;

public class ConfirmUploadCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static ICurrentTenantService CreateTenantService()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(true);
        tenantMock.SetupGet(t => t.DealerId).Returns(DealerId);
        return tenantMock.Object;
    }

    private static IStorageService CreateStorageService()
    {
        var storageMock = new Mock<IStorageService>();
        storageMock.Setup(s => s.GetPresignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Uri("https://example.com/fake-image.jpg"));
        return storageMock.Object;
    }

    private static ConfirmImageUploadCommandHandler CreateHandler(
        TestApplicationDbContext context,
        IStorageService? storage = null) =>
        new(context, storage ?? CreateStorageService(), CreateTenantService(), new FakeDateTimeProvider());

    private static async Task<Car> SeedCarWithImagesAsync(TestApplicationDbContext context, int imageCount)
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);

        var car = new Car(
            DealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 10_000, 2022, "CON001", "Car",
            25000m, DateTime.UtcNow, FuelType.Gasolina, false, Transmission.Manual, null);

        for (int i = 0; i < imageCount; i++)
        {
            var image = CarImage.Create(
                Guid.NewGuid(),
                car.Id,
                $"{DealerId}/{car.Id}/img_{i}.jpg",
                "image/jpeg",
                1024,
                i,
                i == 0);
            car.Images.Add(image);
            context.CarImages.Add(image);
        }

        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    [Fact]
    public async Task Handle_Should_Reject_WhenCarAlreadyHas10Images()
    {
        using var context = CreateContext();
        var car = await SeedCarWithImagesAsync(context, CarImageUploadConstraints.MaxImagesPerCar);
        var handler = CreateHandler(context);

        var command = new ConfirmImageUploadCommand(
            car.Id,
            Guid.NewGuid(),
            "photo11.jpg",
            "image/jpeg",
            2048);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CarImage.LimitReached");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("video/mp4")]
    public async Task Handle_Should_Reject_WhenContentTypeIsInvalid(string invalidContentType)
    {
        using var context = CreateContext();
        var car = await SeedCarWithImagesAsync(context, 2);
        var handler = CreateHandler(context);

        var command = new ConfirmImageUploadCommand(
            car.Id,
            Guid.NewGuid(),
            "doc.pdf",
            invalidContentType,
            2048);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_Should_Reject_WhenSizeExceedsMaximum()
    {
        using var context = CreateContext();
        var car = await SeedCarWithImagesAsync(context, 2);
        var handler = CreateHandler(context);

        long oversized = CarImageUploadConstraints.MaxUploadSizeBytes + 1024;
        var command = new ConfirmImageUploadCommand(
            car.Id,
            Guid.NewGuid(),
            "large.jpg",
            "image/jpeg",
            oversized);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }
}
