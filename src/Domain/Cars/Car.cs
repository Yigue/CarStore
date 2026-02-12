using Domain.Cars.Atribbutes;
using Domain.Cars.Events;
using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Cars;

public sealed class Car : Entity
{
    public Guid MarcaId { get; set; }
    public Marca Marca { get; set; }
    public Guid ModeloId { get; set; }
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
    public LicensePlate Patente { get; private set; }
    public string Descripcion { get; set; }
    public ICollection<CarImage> Images { get; set; } = new List<CarImage>();

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
        Año = año;
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
        statusServiceCar serviceCar,
        int cantidadPuertas,
        int cantidadAsientos,
        int cilindrada,
        int kilometraje,
        int año,
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
        Año = año;
        Patente = new LicensePlate(patente);
        Descripcion = descripcion;
        UpdatedAt = updatedAt;
    }
    
    public void MarkAsSold(DateTime updatedAt)
    {
        if (ServiceCar == statusServiceCar.Vendido)
            return;
        
        ServiceCar = statusServiceCar.Vendido;
        UpdatedAt = updatedAt;
        Raise(new CarSoldDomainEvent(Id));
    }
    
    public void MarkAsAvailable(DateTime updatedAt)
    {
        if (ServiceCar == statusServiceCar.Disponible)
            return;
        
        ServiceCar = statusServiceCar.Disponible;
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
