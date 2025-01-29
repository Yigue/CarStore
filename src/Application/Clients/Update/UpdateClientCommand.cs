using Application.Abstractions.Messaging;
using Domain.Clients.Attributes;

namespace Application.Clients.Update;

public sealed record UpdateClientCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string DNI,
    string Email,
    string Phone,
    string Address,
    ClientStatus Status) : ICommand<Guid>;
