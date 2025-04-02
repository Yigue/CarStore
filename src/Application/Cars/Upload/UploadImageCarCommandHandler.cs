using Application.Cars.Upload;
using Application.Services;
using Domain.Cars;
using Application.Abstractions.Messaging;
using SharedKernel;
using Application.Abstractions.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Application.Cars.Upload;

internal sealed class UploadImageCarCommandHandler(
    IApplicationDbContext context,
    AzureBlobService blobService
) : ICommandHandler<UploadImageCarCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UploadImageCarCommand command, CancellationToken cancellationToken)
    {
        try
        {
            Car? car = await context.Cars.FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

            if (car == null)
            {
                return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));
            }

            // Validar el límite de 10 imágenes
            if (car.Images.Count >= 10)
            {
                return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));

            }

            // Subir la imagen a Azure Blob Storage
            string imageUrl = await blobService.UploadImageAsync(command.ImageData, command.FileName);

            // Crear una nueva entidad CarImage
            var carImage = new CarImage
            {
                CarId = command.CarId,
                ImageUrl = imageUrl,
                IsPrimary = command.IsPrimary,
                Order = command.Order
            };

            // Si se marca como principal, desmarcar las demás imágenes
            if (command.IsPrimary)
            {
                foreach (CarImage image in car.Images)
                {
                    image.IsPrimary = false;
                }
            }

            car.Images.Add(carImage);
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success(carImage.Id);
        }
        catch (System.Exception)
        {

            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));
        }


    }
}
