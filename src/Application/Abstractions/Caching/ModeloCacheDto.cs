using System;

namespace Application.Abstractions.Caching;

public class ModeloCacheDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public Guid MarcaId { get; set; }
}
