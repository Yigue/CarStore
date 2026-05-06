using SharedKernel;

namespace Domain.Cars.Attributes;

public class Modelo : Entity
{
    public string Nombre { get; private set; }
    public Guid MarcaId { get; private set; }
    public Marca Marca { get; set; }

    
      public Modelo(string nombre, Guid marcaId)
    {
        Nombre = nombre;
        MarcaId = marcaId;
        
    }
}
