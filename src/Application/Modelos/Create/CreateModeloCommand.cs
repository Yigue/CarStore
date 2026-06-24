using Application.Abstractions.Messaging;

namespace Application.Modelos.Create;

public sealed record CreateModeloCommand(string Nombre, Guid MarcaId) : ICommand<Guid>;
