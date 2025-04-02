using Domain.Cars.Atribbutes;
using SharedKernel;

namespace Domain.Cars;

public sealed class Car : Entity
{
    public Marca Marca { get; set; }
    public Modelo Modelo { get; set; }
    public Color Color { get; set; }
    public TypeCar CarType { get; set; }
    public StatusCar CarStatus { get; set; }
    public statusServiceCar ServiceCar { get; set; }
    public FuelType FuelType { get; set; }
    public int CantidadPuertas { get; set; }
    public int CantidadAsientos { get; set; }
    public int Cilindrada { get; set; }
    public int Kilometraje { get; set; }
    public int Año { get; set; }
    public string Patente { get; set; }
    public string Descripcion { get; set; }
    public ICollection<CarImage> Images { get; set; } = new List<CarImage>();

    public DateTime CreatedAt { get; set; }
    public decimal Price { get; set; }
    public DateTime UpdatedAt { get; set; }


    private Car()
    {

    }
    public Car(
        Marca marca,
        Modelo modelo,
        Color color,
        TypeCar carType,
        StatusCar carStatus,
        statusServiceCar serviceCar,
        int cantidadPuertas,
        int cantidadAsientos,
        int cilindrada,
        int kilometraje,
        int año,
        string patente,
        string descripcion,
        decimal price,
        DateTime date
        )
    {
        Id = Guid.NewGuid();
        Marca = marca;
        Modelo = modelo;
        Color = color;
        CarType = carType;
        CarStatus = carStatus;
        ServiceCar = serviceCar;
        CantidadPuertas = cantidadPuertas;
        CantidadAsientos = cantidadAsientos;
        Cilindrada = cilindrada;
        Kilometraje = kilometraje;
        Año = año;
        Patente = patente;
        Descripcion = descripcion;
        Price = price;

        CreatedAt = date;
        UpdatedAt = date;
    }


}
