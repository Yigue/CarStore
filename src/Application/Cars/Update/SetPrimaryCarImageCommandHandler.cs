using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Update;

internal sealed class SetPrimaryCarImageCommandHandler : ICommandHandler<SetPrimaryCarImageCommand>
{
    private readonly IApplicationDbContext _context;

    public SetPrimaryCarImageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(SetPrimaryCarImageCommand command, CancellationToken cancellationToken)
    {
        Car car = await _context.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure(CarErrors.NotFound(command.CarId));
        }

        CarImage image = car.Images.FirstOrDefault(i => i.Id == command.ImageId);
        if (image is null)
        {
            return Result.Failure(CarErrors.ImageNotFound(command.ImageId));
        }

        // Establecer todas las imágenes como no primarias
        foreach (CarImage img in car.Images)
        {
            img.IsPrimary = img.Id == command.ImageId;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
} 