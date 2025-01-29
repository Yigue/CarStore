using Application.Abstractions.Messaging;

namespace Application.Clients.Create;

public sealed record CreateClientCommand(
    string FirstName,
    string LastName,
    string DNI,
    string Email,
    string Phone,
    string Address) : ICommand<Guid>;
