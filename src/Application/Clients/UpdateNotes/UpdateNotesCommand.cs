using Application.Abstractions.Messaging;

namespace Application.Clients.UpdateNotes;

public sealed record UpdateNotesCommand(
    Guid Id,
    string? Notes,
    Guid? ActorId) : ICommand;
