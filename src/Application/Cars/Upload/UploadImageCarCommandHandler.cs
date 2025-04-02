using Application.Cars.Upload;
using Application.Services;
using Domain.Cars;
using Application.Abstractions.Messaging;
using SharedKernel;
using Application.Abstractions.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Application.Abstractions.Storage;

namespace Application.Cars.Upload;

internal sealed class UploadImageCarCommandHandler(
    IApplicationDbContext context,
    IBlobStorageService blobStorage)
    : ICommandHandler<UploadImageCarCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UploadImageCarCommand command, CancellationToken cancellationToken)
    {
        Car car = await context.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));
        }

        // Generar nombre único para el archivo
        string uniqueFileName = $"{Guid.NewGuid()}_{command.FileName}";
        
        // Subir imagen a Azure Blob Storage o almacenamiento local
        string imageUrl = await blobStorage.UploadAsync(
            "concardimage", // container
            uniqueFileName,
            command.ImageData,
            cancellationToken);
        
        // Crear la entidad CarImage
        var carImage = new CarImage
        {
            CarId = command.CarId,
            ImageUrl = imageUrl,
            IsPrimary = command.IsPrimary,
            Order = command.Order
        };

        // Si esta imagen es marcada como primaria, actualizar las otras
        if (command.IsPrimary)
        {
            foreach (CarImage img in car.Images.Where(i => i.IsPrimary))
            {
                img.IsPrimary = false;
            }
        }

        // Añadir la imagen a la colección del coche
        car.Images.Add(carImage);
        
        // También añadirla a la tabla de imágenes
        context.CarImages.Add(carImage);
        
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(carImage.Id);
    }
}
