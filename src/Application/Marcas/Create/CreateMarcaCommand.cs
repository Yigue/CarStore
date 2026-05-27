using Application.Abstractions.Messaging;

namespace Application.Marcas.Create;

public sealed record CreateMarcaCommand(string Nombre) : ICommand<Guid>;
