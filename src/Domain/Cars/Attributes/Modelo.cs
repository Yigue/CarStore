using SharedKernel;

namespace Domain.Cars.Attributes;

public class Modelo : Entity
{
    public string Nombre { get; private set; }
    public Guid MarcaId { get; private set; }
    public Marca Marca { get; set; }

    public Modelo(string nombre, Guid marcaId)
    {
        Id = Guid.NewGuid();
        Nombre = nombre;
        MarcaId = marcaId;
    }

    /// <summary>
    /// Factory for seeding with a deterministic ID.
    /// </summary>
    public static Modelo WithId(Guid id, string nombre, Guid marcaId)
    {
        var modelo = new Modelo(nombre, marcaId);
        modelo.Id = id;
        return modelo;
    }
}
