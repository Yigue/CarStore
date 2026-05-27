using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Marcas.Create;

internal sealed class CreateMarcaCommandHandler(IApplicationDbContext context)
    : ICommandHandler<CreateMarcaCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateMarcaCommand command, CancellationToken cancellationToken)
    {
        var nombre = command.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Result.Failure<Guid>(Error.Validation("Marca.NombreRequired", "El nombre de la marca es requerido."));
        }

        var exists = await context.Marca
            .IgnoreQueryFilters()
            .AnyAsync(m => m.Nombre == nombre, cancellationToken);

        if (exists)
        {
            return Result.Failure<Guid>(Error.Conflict("Marca.AlreadyExists", $"Ya existe una marca con el nombre '{nombre}'."));
        }

        var marca = new Marca(nombre);
        context.Marca.Add(marca);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(marca.Id);
    }
}
