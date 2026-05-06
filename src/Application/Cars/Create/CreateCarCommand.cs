using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;

namespace Application.Cars.Create;

public sealed record CreateCarCommand(
    Guid Marca,
    Guid Modelo,
    Color Color,
    TypeCar CarType,
    StatusCar CarStatus,
    StatusServiceCar ServiceCar,
    int CantidadPuertas,
    int CantidadAsientos,
    int Cilindrada,
    int Kilometraje,
    int Anio,
    string Patente,
    string Descripcion,
    decimal Price) : ICommand<Guid>;
