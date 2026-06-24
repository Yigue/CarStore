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

    /// <summary>
    /// Factory for seeding with a deterministic ID.
    /// </summary>
    public static Marca WithId(Guid id, string nombre)
    {
        var marca = new Marca(nombre);
        marca.Id = id;
        return marca;
    }
}
