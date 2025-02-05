using Domain.Cars.Atribbutes;

namespace Application.Cars.GetAll;

public sealed record CarsResponses(
    Guid Id,
    string Marca,
    string Modelo,
    Color Color,
    TypeCar Type,
    StatusCar Status,
    statusServiceCar ServiceStatus,
    int Puertas,
    int Asientos,
    int Cilindrada,
    decimal Kilometraje,
    int Año,
    string Patente,
    string Description,
    decimal Precio,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
