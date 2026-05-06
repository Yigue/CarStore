using SharedKernel;

namespace Domain.Cars;

public class CarImage : Entity
{
    public Guid CarId { get; private set; }
    public string ImageUrl { get; private set; } // URL de la imagen en Azure Blob Storage
    public bool IsPrimary { get; private set; } // Indica si es la imagen principal
    public int Order { get; private set; }     // Orden de la imagen
    public Car Car { get; private set; }

    private CarImage()
    {
    }

    public CarImage(Guid carId, string imageUrl, bool isPrimary, int order)
    {
        Id = Guid.NewGuid();
        CarId = carId;
        ImageUrl = imageUrl;
        IsPrimary = isPrimary;
        Order = order;
    }

    public void SetAsPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }

    public void UpdateImageUrl(string newUrl)
    {
        ImageUrl = newUrl;
    }
}
