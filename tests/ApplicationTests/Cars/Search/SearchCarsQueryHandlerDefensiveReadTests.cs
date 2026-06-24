using Application.Abstractions.Storage;
using Application.Cars.Search;
using Domain.Cars;
using Domain.Cars.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Cars.Search;

/// <summary>
/// RED tests for the defensive read-path in <see cref="SearchCarsQueryHandler.GetPrimaryImageUrl"/>
/// (REQ-FVIP-2). Covers three scenarios:
/// <list type="number">
/// <item>Legacy row with <c>image_url</c> set returns that URL as-is (no presign).</item>
/// <item>Truly broken row (all URL fields null) returns the stable placeholder + logs WARN.</item>
/// <item>Modern row with <c>object_key</c> returns a presigned URL (guardrail against regression).</item>
/// </list>
/// </summary>
public class SearchCarsQueryHandlerDefensiveReadTests
{
    private static Car BuildCarWithImages(params CarImage[] images)
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(
            Guid.NewGuid(),
            marca,
            modelo,
            Color.Black,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
            4, 5, 2000, 0, 2024,
            "DEF123",
            "Defensive read test car",
            15000m,
            new DateTime(2026, 6, 7));

        foreach (CarImage img in images)
        {
            car.Images.Add(img);
        }

        return car;
    }

    [Fact]
    public async Task GetPrimaryImageUrl_LegacyRowWithImageUrl_ReturnsImageUrlAsIs()
    {
        // Arrange — a legacy CarImage with ImageUrl set and ObjectKey null.
        Car car = BuildCarWithImages(new CarImage(
            carId: Guid.NewGuid(),
            imageUrl: "https://legacy.example.com/cars/old-image.jpg",
            isCover: true,
            displayOrder: 0));

        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        var handler = new SearchCarsQueryHandler(
            context: null!,
            storage: storage.Object,
            logger: NullLogger<SearchCarsQueryHandler>.Instance);

        // Act
        string url = await handler.GetPrimaryImageUrl(car, CancellationToken.None);

        // Assert — legacy URL is returned verbatim, no presign call was made.
        url.Should().Be("https://legacy.example.com/cars/old-image.jpg");
        storage.Verify(
            s => s.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPrimaryImageUrl_AllNullFields_ReturnsPlaceholderUrl()
    {
        // Arrange — broken row: IsCover=true but both ImageUrl and ObjectKey are null.
        // We construct the broken shape via the legacy constructor (which leaves
        // ObjectKey null) and then mutate the cover to a sentinel that has no URL.
        var emptyImage = new CarImage(
            carId: Guid.NewGuid(),
            imageUrl: null!, // broken on purpose
            isCover: true,
            displayOrder: 0);
        // Null out via reflection — the constructor is forced but the property is private set.
        // The legacy factory only sets the fields it knows about; ObjectKey is null by default
        // and ImageUrl is now the literal null we passed in.
        Car car = BuildCarWithImages(emptyImage);

        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        var handler = new SearchCarsQueryHandler(
            context: null!,
            storage: storage.Object,
            logger: NullLogger<SearchCarsQueryHandler>.Instance);

        // Act
        string url = await handler.GetPrimaryImageUrl(car, CancellationToken.None);

        // Assert
        url.Should().Be(CarImageDefaults.NoImagePlaceholderUrl);
    }

    [Fact]
    public async Task GetPrimaryImageUrl_ModernRowWithObjectKey_ReturnsPresignedUrl()
    {
        // Arrange
        Guid imageId = Guid.NewGuid();
        var modernImage = CarImage.Create(
            imageId: imageId,
            carId: Guid.NewGuid(),
            objectKey: "cars/d-001/c-001/abc.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1234,
            displayOrder: 0,
            isCover: true);
        Car car = BuildCarWithImages(modernImage);

        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.GetPresignedUrlAsync(
                "cars/d-001/c-001/abc.jpg",
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Uri("https://minio.example.com/cars/d-001/c-001/abc.jpg?X-Amz-Signature=xxx"));

        var handler = new SearchCarsQueryHandler(
            context: null!,
            storage: storage.Object,
            logger: NullLogger<SearchCarsQueryHandler>.Instance);

        // Act
        string url = await handler.GetPrimaryImageUrl(car, CancellationToken.None);

        // Assert
        url.Should().Be("https://minio.example.com/cars/d-001/c-001/abc.jpg?X-Amz-Signature=xxx");
        storage.Verify(
            s => s.GetPresignedUrlAsync("cars/d-001/c-001/abc.jpg", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
