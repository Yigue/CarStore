using System;

namespace Application.Abstractions.Caching;

public class MarcaCacheDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
