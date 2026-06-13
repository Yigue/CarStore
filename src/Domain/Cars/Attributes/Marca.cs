using SharedKernel;
namespace Domain.Cars.Attributes;

// Domain/Cars/Attributes/Marca.cs
public class Marca : Entity
{
    public string Nombre { get; private set; }
    public ICollection<Modelo> Modelos { get; private set; }
    public Marca(string nombre)
    {
        Id = Guid.NewGuid();
        Nombre = nombre;
        Modelos = new List<Modelo>();
    }

}
