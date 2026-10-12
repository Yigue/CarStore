using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Cars.Commands.ConfirmImageUpload;
using Application.Common.Files;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Common;

/// <summary>
/// A5.1 / RN-035 / H11: the confirm step must trust the object's real signature, not the
/// Content-Type the client declared.
/// </summary>
public class FileSignatureInspectorTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] Webp = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] Exe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00];

    [Fact]
    public void Inspect_Should_DetectJpeg() =>
        FileSignatureInspector.IsSupportedImage(Jpeg).Should().BeTrue();

    [Fact]
    public void Inspect_Should_DetectPng() =>
        FileSignatureInspector.IsSupportedImage(Png).Should().BeTrue();

    [Fact]
    public void Inspect_Should_DetectWebp() =>
        FileSignatureInspector.IsSupportedImage(Webp).Should().BeTrue();

    [Fact]
    public void Inspect_Should_RejectExecutable() =>
        FileSignatureInspector.IsSupportedImage(Exe).Should().BeFalse();

    [Fact]
    public void Inspect_Should_RejectRiffThatIsNotWebp()
    {
        // WAV also starts with RIFF; bytes 8..11 are "WAVE".
        byte[] wav = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45];
        FileSignatureInspector.IsSupportedImage(wav).Should().BeFalse();
    }

    [Fact]
    public void Inspect_Should_RejectEmptyAndTruncated()
    {
        FileSignatureInspector.IsSupportedImage([]).Should().BeFalse();
        FileSignatureInspector.IsSupportedImage([0xFF, 0xD8]).Should().BeFalse();
        FileSignatureInspector.IsSupportedImage([0x89, 0x50, 0x4E, 0x47]).Should().BeFalse();
    }

    [Fact]
    public async Task Confirm_Should_Accept_PngBytes_DeclaredAsJpeg()
    {
        var (handler, storage, car) = await ArrangeAsync(Png);

        var result = await handler.Handle(
            new ConfirmImageUploadCommand(car.Id, Guid.NewGuid(), "photo.jpg", "image/jpeg", 2048),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_Should_RejectAndDeleteObject_WhenExeRenamedToJpg()
    {
        var (handler, storage, car) = await ArrangeAsync(Exe);

        var result = await handler.Handle(
            new ConfirmImageUploadCommand(car.Id, Guid.NewGuid(), "evil.jpg", "image/jpeg", 2048),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CarImage.InvalidSignature");
        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_Should_RejectAndDeleteObject_WhenObjectIsMissing()
    {
        var (handler, storage, car) = await ArrangeAsync(null);

        var result = await handler.Handle(
            new ConfirmImageUploadCommand(car.Id, Guid.NewGuid(), "ghost.jpg", "image/jpeg", 2048),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CarImage.InvalidSignature");
        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_Should_StillRejectDisallowedContentType_EvenWithValidSignature()
    {
        var (handler, _, car) = await ArrangeAsync(Png);

        var result = await handler.Handle(
            new ConfirmImageUploadCommand(car.Id, Guid.NewGuid(), "x.svg", "image/svg+xml", 2048),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CarImage.InvalidContentType");
    }

    private static async Task<(ConfirmImageUploadCommandHandler Handler, Mock<IStorageService> Storage, Car Car)>
        ArrangeAsync(byte[]? head)
    {
        var dealerId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new TestApplicationDbContext(options);

        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        var car = new Car(
            dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 10_000, 2022, "SIG001", "Car",
            25000m, DateTime.UtcNow, FuelType.Gasolina, false, Transmission.Manual, null);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.ReadHeadAsync(It.IsAny<string>(), 16, It.IsAny<CancellationToken>()))
            .ReturnsAsync(head);
        storage.Setup(s => s.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Uri("https://example.com/fake-image.jpg"));

        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);

        var handler = new ConfirmImageUploadCommandHandler(
            context, storage.Object, tenant.Object, new FakeDateTimeProvider());
        return (handler, storage, car);
    }
}
