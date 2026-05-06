using Domain.Cars.Attributes;
using Domain.Cars.Events;
using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Cars;

public sealed class Car : Entity
{
    public Guid MarcaId { get; private set; }
    public Marca Marca { get; private set; }
    public Guid ModeloId { get; private set; }
    public Modelo Modelo { get; private set; }
    public Color Color { get; private set; }
    public TypeCar CarType { get; private set; }
    public StatusCar CarStatus { get; private set; }
    public StatusServiceCar ServiceCar { get; private set; }
    public FuelType FuelType { get; private set; }
    public int CantidadPuertas { get; private set; }
    public int CantidadAsientos { get; private set; }
    public int Cilindrada { get; private set; }
    public int Kilometraje { get; private set; }
    public int Anio { get; private set; }
    public LicensePlate Patente { get; private set; }
    public string Descripcion { get; private set; }
    public ICollection<CarImage> Images { get; private set; } = new List<CarImage>();

    public DateTime CreatedAt { get; private set; }
    public Money Price { get; private set; }
    public DateTime UpdatedAt { get; private set; }


    private Car()
    {
        Images = new List<CarImage>();
    }
    
    public Car(
        Guid dealerId,
        Marca marca,
        Modelo modelo,
        Color color,
        TypeCar carType,
        StatusCar carStatus,
        StatusServiceCar serviceCar,
        int cantidadPuertas,
        int cantidadAsientos,
        int cilindrada,
        int kilometraje,
        int anio,
        string patente,
        string descripcion,
        decimal price,
        DateTime date
        )
    {
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        Marca = marca;
        MarcaId = marca.Id;
        Modelo = modelo;
        ModeloId = modelo.Id;
        Color = color;
        CarType = carType;
        CarStatus = carStatus;
        ServiceCar = serviceCar;
        CantidadPuertas = cantidadPuertas;
        CantidadAsientos = cantidadAsientos;
        Cilindrada = cilindrada;
        Kilometraje = kilometraje;
        Anio = anio;
        Patente = new LicensePlate(patente);
        Descripcion = descripcion;
        Price = new Money(price);

        CreatedAt = date;
        UpdatedAt = date;

        Raise(new NewCarDomainEvent(Id));
    }

    public void UpdateDetails(
        Marca marca,
        Modelo modelo,
        Color color,
        TypeCar carType,
        StatusCar carStatus,
        StatusServiceCar serviceCar,
        int cantidadPuertas,
        int cantidadAsientos,
        int cilindrada,
        int kilometraje,
        int anio,
        string patente,
        string descripcion,
        DateTime updatedAt)
    {
        Marca = marca;
        MarcaId = marca.Id;
        Modelo = modelo;
        ModeloId = modelo.Id;
        Color = color;
        CarType = carType;
        CarStatus = carStatus;
        ServiceCar = serviceCar;
        CantidadPuertas = cantidadPuertas;
        CantidadAsientos = cantidadAsientos;
        Cilindrada = cilindrada;
        Kilometraje = kilometraje;
        Anio = anio;
        Patente = new LicensePlate(patente);
        Descripcion = descripcion;
        UpdatedAt = updatedAt;
    }
    
    public void MarkAsSold(DateTime updatedAt)
    {
        if (ServiceCar == StatusServiceCar.Vendido)
            return;
        
        ServiceCar = StatusServiceCar.Vendido;
        UpdatedAt = updatedAt;
        Raise(new CarSoldDomainEvent(Id));
    }
    
    public void MarkAsAvailable(DateTime updatedAt)
    {
        if (ServiceCar == StatusServiceCar.Disponible)
            return;
        
        ServiceCar = StatusServiceCar.Disponible;
        UpdatedAt = updatedAt;
    }
    
    public void UpdatePrice(decimal newPrice, DateTime updatedAt)
    {
        Price = new Money(newPrice);
        UpdatedAt = updatedAt;
    }
    
    public void UpdatePrice(Money newPrice, DateTime updatedAt)
    {
        Price = newPrice;
        UpdatedAt = updatedAt;
    }
}
