using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Modelos.Create;

internal sealed class CreateModeloCommandHandler(
    IApplicationDbContext context,
    ICachedModelService modelService)
    : ICommandHandler<CreateModeloCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateModeloCommand command, CancellationToken cancellationToken)
    {
        var nombre = command.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Result.Failure<Guid>(Error.Validation("Modelo.NombreRequired", "El nombre del modelo es requerido."));
        }

        // Verify marca exists
        var marcaExists = await context.Marca
            .IgnoreQueryFilters()
            .AnyAsync(m => m.Id == command.MarcaId, cancellationToken);

        if (!marcaExists)
        {
            return Result.Failure<Guid>(Error.NotFound("Marca.NotFound", $"La marca con ID '{command.MarcaId}' no existe."));
        }

        var exists = await context.Modelo
            .IgnoreQueryFilters()
            .AnyAsync(m => m.Nombre == nombre && m.MarcaId == command.MarcaId, cancellationToken);

        if (exists)
        {
            return Result.Failure<Guid>(Error.Conflict("Modelo.AlreadyExists", $"Ya existe un modelo '{nombre}' para esta marca."));
        }

        var modelo = new Modelo(nombre, command.MarcaId);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync(cancellationToken);

        // Invalidate cached models for this brand
        await modelService.InvalidateBrandCacheAsync(command.MarcaId, cancellationToken);

        return Result.Success(modelo.Id);
    }
}
