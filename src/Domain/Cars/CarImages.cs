using SharedKernel;

namespace Domain.Cars;

public class CarImage:Entity
{

    public Guid CarId { get; set; }
    public string ImageUrl { get; set; } // URL de la imagen en Azure Blob Storage
    public bool IsPrimary { get; set; } // Indica si es la imagen principal
    public int Order { get; set; }     // Orden de la imagen
    public Car Car { get; set; }

    public CarImage()
    {
        
    }
}
