using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Attributes;

namespace Application.Cars.Update;

public sealed record UpdateCarCommand(
    Guid Id,
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
    decimal Price,
    FuelType FuelType = FuelType.Gasolina,
    bool Featured = false,
    Transmission Transmission = Transmission.Manual,
    decimal? PurchaseCost = null) : ICommand<Guid>;
