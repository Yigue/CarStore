using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Cars.Queries.GetCarImages;
using Application.Cars.RegenerateImageUrls;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Cars;

public class CarImagesTests
{
    private static readonly Guid DealerA = Guid.NewGuid();
    private static readonly Guid DealerB = Guid.NewGuid();

    private static ApplicationDbContext CreateContext(Guid dealerId, bool hasTenant = true, string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(dealerId);
        tenantService.Setup(t => t.HasTenant).Returns(hasTenant);

        return new ApplicationDbContext(options, Mock.Of<IPublisher>(), tenantService.Object);
    }

    [Fact]
    public async Task RegenerateImageUrls_Should_OnlyUpdateImagesOfCurrentTenant()
    {
        // Arrange: Db with cars from Dealer A and Dealer B
        string dbName = Guid.NewGuid().ToString();

        // Seed data using a platform/admin context without tenant filter
        using (var seedContext = CreateContext(Guid.Empty, hasTenant: false, dbName))
        {
            var marca = new Marca("Toyota");
            var modelo = new Modelo("Corolla", marca.Id);
            seedContext.Marca.Add(marca);
            seedContext.Modelo.Add(modelo);

            var carA = new Car(
                DealerA, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.Used,
                StatusServiceCar.Disponible, 4, 5, 1600, 10_000, 2022, "AAA111", "Car A",
                25000m, DateTime.UtcNow, FuelType.Gasolina, false, Transmission.Manual, null);

            var carB = new Car(
                DealerB, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.Used,
                StatusServiceCar.Disponible, 4, 5, 1600, 10_000, 2022, "BBB222", "Car B",
                25000m, DateTime.UtcNow, FuelType.Gasolina, false, Transmission.Manual, null);

            seedContext.Cars.AddRange(carA, carB);

            var imgA = new CarImage(carA.Id, "https://storage.blob.core.windows.net/cars/imgA.jpg", true, 0);
            var imgB = new CarImage(carB.Id, "https://storage.blob.core.windows.net/cars/imgB.jpg", true, 0);

            carA.Images.Add(imgA);
            carB.Images.Add(imgB);

            await seedContext.SaveChangesAsync();
        }

        // Act: Execute RegenerateImageUrlsHandler under Dealer A tenant context
        using (var tenantAContext = CreateContext(DealerA, hasTenant: true, dbName))
        {
            var mockBlob = new Mock<IBlobStorageService>();
            mockBlob.Setup(b => b.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            mockBlob.Setup(b => b.GenerateSasUrl(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("https://storage.blob.core.windows.net/cars/new-sas-url");

            var logger = Mock.Of<ILogger<RegenerateImageUrlsHandler>>();
            var handler = new RegenerateImageUrlsHandler(tenantAContext, mockBlob.Object, logger);

            var result = await handler.Handle(new RegenerateImageUrlsCommand(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            // Only 1 image (Dealer A's) should be updated, NOT Dealer B's image!
            result.Value.Should().Be(1);
        }

        // Verify: Dealer B's image was not modified
        using (var verifyContext = CreateContext(Guid.Empty, hasTenant: false, dbName))
        {
            var carB = await verifyContext.Cars.Include(c => c.Images).FirstAsync(c => c.DealerId == DealerB);
            var imgB = carB.Images.First();
            imgB.ImageUrl.Should().Be("https://storage.blob.core.windows.net/cars/imgB.jpg");
        }
    }

    [Fact]
    public async Task ImagesQuery_AnchoredOnCars_Should_NotReturnImagesOfOtherDealer()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        Guid carBId;

        using (var seedContext = CreateContext(Guid.Empty, hasTenant: false, dbName))
        {
            var marca = new Marca("Ford");
            var modelo = new Modelo("Focus", marca.Id);
            seedContext.Marca.Add(marca);
            seedContext.Modelo.Add(modelo);

            var carB = new Car(
                DealerB, marca, modelo, Color.Black, TypeCar.Hatchback, StatusCar.Used,
                StatusServiceCar.Disponible, 4, 5, 1600, 20_000, 2021, "CCC333", "Car B",
                20000m, DateTime.UtcNow, FuelType.Gasolina, false, Transmission.Manual, null);
            seedContext.Cars.Add(carB);

            var imgB = new CarImage(carB.Id, "https://storage.blob.core.windows.net/cars/imgB.jpg", true, 0);
            carB.Images.Add(imgB);

            await seedContext.SaveChangesAsync();
            carBId = carB.Id;
        }

        // Act & Assert: Under Dealer A, querying images anchored on Cars yields empty/notFound
        using (var tenantAContext = CreateContext(DealerA, hasTenant: true, dbName))
        {
            var imagesOfCarB = await tenantAContext.Cars
                .Where(c => c.Id == carBId)
                .SelectMany(c => c.Images)
                .ToListAsync();

            imagesOfCarB.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task PrimaryImagesQuery_Should_PaginateAndFilterByTenant()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();

        using (var seedContext = CreateContext(Guid.Empty, hasTenant: false, dbName))
        {
            var marca = new Marca("Renault");
            var modelo = new Modelo("Clio", marca.Id);
            seedContext.Marca.Add(marca);
            seedContext.Modelo.Add(modelo);

            // 3 cars for Dealer A with cover images
            for (int i = 1; i <= 3; i++)
            {
                var carA = new Car(
                    DealerA, marca, modelo, Color.White, TypeCar.Hatchback, StatusCar.Used,
                    StatusServiceCar.Disponible, 4, 5, 1200, 30_000, 2020, $"AA00{i}AA", $"Car A {i}",
                    15000m, DateTime.UtcNow, FuelType.Gasolina, false, Transmission.Manual, null);
                seedContext.Cars.Add(carA);
                carA.Images.Add(new CarImage(carA.Id, $"https://storage/cars/a_{i}.jpg", true, 0));
            }

            // 1 car for Dealer B with cover image
            var carB = new Car(
                DealerB, marca, modelo, Color.White, TypeCar.Hatchback, StatusCar.Used,
                StatusServiceCar.Disponible, 4, 5, 1200, 30_000, 2020, "BB001BB", "Car B 1",
                15000m, DateTime.UtcNow, FuelType.Gasolina, false, Transmission.Manual, null);
            seedContext.Cars.Add(carB);
            carB.Images.Add(new CarImage(carB.Id, "https://storage/cars/b_1.jpg", true, 0));

            await seedContext.SaveChangesAsync();
        }

        // Act: Page 1 with pageSize = 2 under Dealer A
        using (var tenantAContext = CreateContext(DealerA, hasTenant: true, dbName))
        {
            int page = 1;
            int pageSize = 2;

            var pagedImages = await tenantAContext.Cars
                .SelectMany(c => c.Images)
                .Where(img => img.IsCover)
                .OrderBy(img => img.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Assert: Exactly 2 images from Dealer A, none from Dealer B
            pagedImages.Should().HaveCount(2);
            pagedImages.Should().OnlyContain(img => img.ImageUrl!.Contains("/a_"));
        }
    }
}
