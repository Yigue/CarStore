using Application.Abstractions.Messaging;
using Domain.Cars.Atribbutes;

namespace Application.Cars.Create;

public sealed class CreateCarCommand : ICommand<Guid>
{
    public Marca Marca { get; set; }
    public Modelo Modelo { get; set; }
    public Color Color { get; set; }
    public TypeCar CarType { get; set; }
    public StatusCar CarStatus { get; set; }
    public statusServiceCar ServiceCar { get; set; }
    public int CantidadPuertas { get; set; }
    public int CantidadAsientos { get; set; }
    public int Cilindrada { get; set; }
    public int Kilometraje { get; set; }
    public int Anio { get; set; }
    public string Patente { get; set; }
    public string Descripcion { get; set; }
    public decimal Price { get; set; }
}
 