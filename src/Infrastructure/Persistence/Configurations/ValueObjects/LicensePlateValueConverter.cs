using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence.Configurations.ValueObjects;

/// <summary>
/// Value converter para LicensePlate. Convierte LicensePlate a string para almacenamiento en BD.
/// NOTA: Para activar este converter, actualizar las configuraciones de entidades
/// y crear una migración. Coordinar con Rol 3 (DevOps/Infrastructure).
/// </summary>
public class LicensePlateValueConverter : ValueConverter<LicensePlate, string>
{
    public LicensePlateValueConverter() : base(
        licensePlate => licensePlate.Value,
        value => new LicensePlate(value))
    {
    }
}

