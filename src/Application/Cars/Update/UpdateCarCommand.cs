using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Atribbutes;

namespace Application.Cars.Update;

public sealed record UpdateCarCommand(
    Guid Id,
    Marca Marca,
    Modelo Modelo,
    Color Color,
    TypeCar CarType,
    StatusCar CarStatus,
    statusServiceCar ServiceCar,
    int CantidadPuertas,
    int CantidadAsientos,
    int Cilindrada,
    int Kilometraje,
    int Anio,
    string Patente,
    string Descripcion,
    decimal Price) : ICommand<Guid>;
