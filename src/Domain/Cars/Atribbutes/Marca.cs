using SharedKernel;
namespace Domain.Cars.Atribbutes;

// Domain/Cars/Attributes/Marca.cs
public class Marca : Entity
{
    public string Nombre { get; private set; }
    public ICollection<Modelo> Modelos { get; private set; }
    public Marca(string nombre)
    {
        Nombre = nombre;
        Modelos = new List<Modelo>();
    }

}
