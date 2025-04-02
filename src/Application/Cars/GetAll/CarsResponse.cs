using Domain.Cars.Atribbutes;
using Domain.Cars;
using System.Diagnostics.CodeAnalysis;

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
    DateTime UpdatedAt,
    List<CarImageResponse> Images
);

[SuppressMessage("Design", "CA1054:Los parámetros URI deben ser de tipo System.Uri", Justification = "Se usa string para simplificar")]
public sealed record CarImageResponse(
    Guid Id,
    string ImageUrl,
    bool IsPrimary,
    int Order
);
