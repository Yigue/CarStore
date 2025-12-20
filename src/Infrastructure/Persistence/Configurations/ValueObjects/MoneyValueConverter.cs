using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Persistence.Configurations.ValueObjects;

/// <summary>
/// Value converter para Money. Convierte Money a decimal para almacenamiento en BD.
/// NOTA: Para activar este converter, actualizar las configuraciones de entidades
/// y crear una migración. Coordinar con Rol 3 (DevOps/Infrastructure).
/// </summary>
public class MoneyValueConverter : ValueConverter<Money, decimal>
{
    public MoneyValueConverter() : base(
        money => money.Amount,
        amount => new Money(amount))
    {
    }
}

