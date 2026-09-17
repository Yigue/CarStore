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
    decimal? PurchaseCost = null,
    /// <summary>
    /// INV-01: which of the car's images is the cover. Null means "leave the cover as it is" —
    /// the field is optional so every existing caller keeps working unchanged, and an update
    /// that says nothing about images never disturbs them.
    /// </summary>
    Guid? CoverImageId = null) : ICommand<Guid>;
