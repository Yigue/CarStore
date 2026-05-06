using Domain.Cars.Attributes;

namespace Application.Cars.GetById;

public sealed record CarGetByIdResponse(
    Guid Id,
    string Marca,
    string Modelo,
    Color Color,
    TypeCar Type,
    StatusCar Status,
    StatusServiceCar ServiceStatus,
    int Puertas,
    int Asientos,
    int Cilindrada,
    int Kilometraje,
    int Anio,
    string Patente,
    string Description,
    decimal Precio,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
