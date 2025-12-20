using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence.Configurations.ValueObjects;

/// <summary>
/// Value converter para Email. Convierte Email a string para almacenamiento en BD.
/// NOTA: Para activar este converter, actualizar las configuraciones de entidades
/// y crear una migración. Coordinar con Rol 3 (DevOps/Infrastructure).
/// </summary>
public class EmailValueConverter : ValueConverter<Email, string>
{
    public EmailValueConverter() : base(
        email => email.Value,
        value => new Email(value))
    {
    }
}

