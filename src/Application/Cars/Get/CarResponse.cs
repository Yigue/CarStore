using Domain.Cars.Attributes;

namespace Application.Cars.Get;

public sealed class CarResponse
{
    public Guid Id { get; init; }
    public Guid MarcaId { get; init; }
    public string Marca { get; init; } = string.Empty;
    public Guid ModeloId { get; init; }
    public string Modelo { get; init; } = string.Empty;
    public Color Color { get; init; }
    public TypeCar Type { get; init; }
    public StatusCar Status { get; init; }
    public StatusServiceCar ServiceStatus { get; init; }
    public int Puertas { get; init; }
    public int Asientos { get; init; }
    public int Cilindrada { get; init; }
    public int Kilometraje { get; init; }
    public int Anio { get; init; }
    public string Patente { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Precio { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<CarImageResponse> Images { get; init; } = new();
}

public sealed class CarImageResponse
{
    public Guid Id { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public int Order { get; init; }
}
